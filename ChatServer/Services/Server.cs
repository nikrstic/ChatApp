using ChatServer.Models;
using ChatShared;
using ChatShared.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace ChatServer.Services
{
    public class Server
    {
        // atomic id (Interlocked.Increment)
        static int ID = 100;

        // thread-safe client store
        static ConcurrentDictionary<Socket, ClientInfo> clients = new ConcurrentDictionary<Socket, ClientInfo>();

        // accept loop signal
        public static ManualResetEvent acceptCompleted = new ManualResetEvent(false);

        public static void Start(int port)
        {
            IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
            var ip = ipHost.AddressList.First(a => a.AddressFamily == AddressFamily.InterNetwork);
            IPEndPoint remoteEndPoint = new IPEndPoint(ip, port);

            Socket listener = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(remoteEndPoint);
            listener.Listen(100);

            Console.WriteLine($"Server started on {remoteEndPoint}. Waiting for connections...");

            while (true)
            {
                acceptCompleted.Reset();
                listener.BeginAccept(new AsyncCallback(AcceptCallBack), listener);
                acceptCompleted.WaitOne();
            }
        }

        public static void AcceptCallBack(IAsyncResult ar)
        {
            try
            {
                acceptCompleted.Set();
                Socket listener = ar.AsyncState as Socket;
                Socket handler = listener.EndAccept(ar);

                Console.WriteLine($"Incoming connection from {handler.RemoteEndPoint}");

                ObjectState state = new ObjectState();
                state.wSocket = handler;

                handler.BeginReceive(state.buffer, 0, ObjectState.bufferSize, 0, new AsyncCallback(ReadCallBack), state);
            }
            catch (Exception ex)
            {
                Console.WriteLine("AcceptCallBack error: " + ex.Message);
            }
        }

        // per-client state
        private class ObjectState
        {
            public const int bufferSize = 4096;
            public byte[] buffer = new byte[bufferSize];
            public StringBuilder sb = new StringBuilder();
            public Socket wSocket;
        }

        // kada stigne nesto od klijenta
        public static void ReadCallBack(IAsyncResult ar)
        {
            ObjectState state = (ObjectState)ar.AsyncState;
            Socket handler = state.wSocket;

            try
            {
                int bytesRead = handler.EndReceive(ar);

                if (bytesRead <= 0)
                {
                    // konekcija zatvorena
                    Disconnect(handler);
                    return;
                }

                // append primljene bajtove (UTF8)
                state.sb.Append(Encoding.UTF8.GetString(state.buffer, 0, bytesRead));
                string content = state.sb.ToString();

                int idx = 0;

                // obradi sve kompletne frame-ove koji sadrze "<EOF>"
                while ((idx = content.IndexOf("<EOF>", StringComparison.Ordinal)) >= 0)
                {
                    string frame = content.Substring(0, idx);
                    content = content.Substring(idx + 5); // preskoci "<EOF>"

                    if (string.IsNullOrWhiteSpace(frame))
                        continue;

                    try
                    {
                        var msg = JsonSerializer.Deserialize<Message>(frame);
                        if (msg == null)
                            continue;
                        Console.WriteLine("poruka: " + frame);
                        // Ako novi klijent (još nije u clients) i šalje join:
                        if (!clients.ContainsKey(handler) && string.Equals(msg.Type, "join", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine("ovde smo server");
                            string nickname = msg.From ?? $"User{Interlocked.Increment(ref ID)}";
                            var info = new ClientInfo { Socket = handler, ID = Interlocked.Increment(ref ID), Nickname = nickname };

                            clients[handler] = info;

                            // send init samo tom klijentu (liste i welcome)
                            var init = new
                            {
                                Type = "init",
                                Users = clients.Values.Select(c => c.Nickname).ToList(),
                                Welcome = $"Dobrodošao, {nickname}!"
                            };
                            Console.WriteLine("handler i init: "+ handler.ToString() + " " + init);
                            Send(handler, JsonSerializer.Serialize(init) + "<EOF>");
                            Thread.Sleep(50);
                            // obavesti ostale da se pridruzio
                            var joined = new { Type = "user_joined", User = nickname };
                            Broadcast(JsonSerializer.Serialize(joined) + "<EOF>", handler);

                            Console.WriteLine($"User joined: {nickname} ({handler.RemoteEndPoint})");
                        }
                        else if (string.Equals(msg.Type, "message", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!clients.TryGetValue(handler, out var sender))
                            {
                                Console.WriteLine("Received message from unknown client - ignoring");
                            }
                            else
                            {
                                var broadcastMsg = new Message
                                {
                                    Type = "message",
                                    From = sender.Nickname,
                                    Text = msg.Text
                                };
                                Broadcast(JsonSerializer.Serialize(broadcastMsg) + "<EOF>", handler);
                                Console.WriteLine($"Broadcast message from {sender.Nickname}: {msg.Text}");
                            }
                        }
                        else if (string.Equals(msg.Type, "privateMessage", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!clients.TryGetValue(handler, out var sender))
                            {
                                Console.WriteLine("Received privateMessage from unknown client - ignoring");
                            }
                            else
                            {
                                var pm = new Message
                                {
                                    Type = "privateMessage",
                                    From = sender.Nickname,
                                    To = msg.To,
                                    Text = msg.Text
                                };

                                string pmJson = JsonSerializer.Serialize(pm) + "<EOF>";
                                var target = clients.Values.FirstOrDefault(c => c.Nickname == msg.To);
                                if (target != null)
                                {
                                    Send(target.Socket, pmJson);
                                }

                                // kopija posiljaocu
                                Send(handler, pmJson);

                                Console.WriteLine($"Private from {sender.Nickname} to {msg.To}: {msg.Text}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Unknown message type: " + msg.Type);
                        }
                    }
                    catch (JsonException)
                    {
                        // frame nije kompletan ili je loš JSON: vratimo ga u buffer i čekamo još podatka
                        content = frame + "<EOF>" + content;
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Processing error: " + ex.Message);
                    }
                }

                // ostavi nepotpun sadržaj u bufferu
                state.sb.Clear();
                state.sb.Append(content);

                // nastavi da primaš
                handler.BeginReceive(state.buffer, 0, ObjectState.bufferSize, 0, new AsyncCallback(ReadCallBack), state);
            }
            catch (SocketException)
            {
                Disconnect(handler);
            }
            catch (ObjectDisposedException)
            {
                // socket zatvoren
                Disconnect(handler);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ReadCallBack error: " + ex.Message);
                Disconnect(handler);
            }
        }

        private static void Disconnect(Socket handler)
        {
            if (handler == null) return;

            try
            {
                if (clients.TryRemove(handler, out var user))
                {
                    Console.WriteLine($"Client disconnected: {user.Nickname}");
                    ChatShared.Models.ActiveClients.Remove(user.Nickname);

                    var leftMsg = new { Type = "user_left", User = user.Nickname };
                    Broadcast(JsonSerializer.Serialize(leftMsg) + "<EOF>", handler);
                }

                try { handler.Shutdown(SocketShutdown.Both); } catch { }
                handler.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Disconnect error: " + ex.Message);
            }
        }

        private static void Broadcast(string message, Socket sender)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);

            foreach (var kv in clients)
            {
                var sock = kv.Key;
                if (sock == null) continue;
                if (sock == sender) continue;

                try
                {
                    sock.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallBack), sock);
                }
                catch (SocketException ex)
                {
                    Console.WriteLine("Broadcast send failed: " + ex.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Broadcast error: " + ex.Message);
                }
            }
        }

        private static void Send(Socket handler, string content)
        {
            if (handler == null) return;

            byte[] byteData = Encoding.UTF8.GetBytes(content);
            try
            {
                handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallBack), handler);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Send error: " + ex.Message);
            }
        }

        private static void SendCallBack(IAsyncResult ar)
        {
            try
            {
                Socket handler = (Socket)ar.AsyncState;
                int bytes = handler.EndSend(ar);
                // najmanje log da ne zatrpavamo konzolu
                Console.WriteLine($"Sent {bytes} bytes to {handler.RemoteEndPoint}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("SendCallBack error: " + ex.Message);
            }
        }
    }
}
