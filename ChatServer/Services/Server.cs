using ChatServer.Models;
using ChatShared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ChatServer.Services
{
    class Server
    {
        static int ID = 100;
        static Dictionary<Socket, ClientInfo> klijenti = new Dictionary<Socket, ClientInfo>();
        public static ManualResetEvent complited = new ManualResetEvent(false);

        public static void Start(int port)
        {
            IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
            var ip = ipHost.AddressList.First(a => a.AddressFamily == AddressFamily.InterNetwork);
            //IPAddress ip = ipHost.AddressList[0];
            IPEndPoint remoteEndPoint = new IPEndPoint(ip, port);

            Socket listener = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(remoteEndPoint);
            listener.Listen(100);

            while (true)
            {
                complited.Reset();
                listener.BeginAccept(new AsyncCallback(AcceptCallBack), listener);
                complited.WaitOne();
            }
        }

        public static void AcceptCallBack(IAsyncResult ar)
        {
            complited.Set();
            Socket listener = ar.AsyncState as Socket;
            Socket handler = listener.EndAccept(ar);

            ObjectState state = new ObjectState();
            state.wSocket = handler;

            handler.BeginReceive(state.buffer, 0, ObjectState.bufferSize, 0, new AsyncCallback(ReadCallBack), state);
        }

        public static void ReadCallBack(IAsyncResult ar)
        {
            ObjectState state = (ObjectState)ar.AsyncState;
            Socket handler = state.wSocket;

            try
            {
                int bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0)
                {
                    state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));
                    string content = state.sb.ToString();

                    if (content.IndexOf("<EOF>", StringComparison.Ordinal) > -1)
                    {
                        string cleanMessage = content.Replace("<EOF>", "").Trim();
                        Message msg = JsonSerializer.Deserialize<Message>(cleanMessage);


                        if (!klijenti.ContainsKey(handler))
                        {
                            // Prva poruka je nickname
                            string nickname = msg.From;
                            ClientInfo info = new ClientInfo
                            {
                                Socket = handler,
                                ID = ID++,
                                Nickname = nickname
                            };
                            lock (klijenti)
                            {
                                klijenti.Add(handler, info);
                            }

                            //Console.WriteLine($"Klijent povezan: {nickname} ({handler.RemoteEndPoint})");
                            Send(handler, JsonSerializer.Serialize(new Message
                            {
                                Type = "system",
                                Text = $"Dobrodošao, {info.Nickname}!"
                            }) + "<EOF>");

                        }
                        else if (msg.Type.Equals("message"))
                        {
                            ClientInfo sender = klijenti[handler];
                            Console.WriteLine($"[{sender.Nickname}]: {msg.Text}");

                            Message broadcastMsg = new Message
                            {
                                Type = "message",
                                From = sender.Nickname,
                                Text = msg.Text
                            };

                            string broadcastJson = JsonSerializer.Serialize(broadcastMsg) + "<EOF>";
                            Broadcast(broadcastJson, handler);

                        }

                        state.sb.Clear();
                    }

                    handler.BeginReceive(state.buffer, 0, ObjectState.bufferSize, 0, new AsyncCallback(ReadCallBack), state);
                }
            }
            catch (Exception ex)
            {
                Disconnect(handler);
                Console.WriteLine($"Greška u ReadCallBack: {ex.Message}");
            }
        }
        private static void Disconnect(Socket handler)
        {
            lock (klijenti)
            {
                if (klijenti.ContainsKey(handler))
                {
                    var user = klijenti[handler];

                    Console.WriteLine($"klijent {user.Nickname} se diskonektovao. ");
                    klijenti.Remove(handler);

                    Message msg = new Message
                    {
                        Type = "system",
                        Text = $"{user.Nickname} je napustio chat. "


                    };

                    Broadcast(JsonSerializer.Serialize(msg) + "<EOF>", handler);

                }
            }
            try
            {
                handler.Shutdown(SocketShutdown.Both);
            }
            catch { }
            handler.Close();
        }
        private static void Broadcast(string message, Socket sender)
        {
            byte[] data = Encoding.ASCII.GetBytes(message);

            lock (klijenti)
            {
                foreach (var klijent in klijenti)
                {
                    if (klijent.Key != sender)
                    {
                        klijent.Key.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallBack), klijent.Key);
                    }
                }
            }
        }

        private static void Send(Socket handler, string content)
        {
            byte[] byteData = Encoding.ASCII.GetBytes(content);
            handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallBack), handler);
        }

        private static void SendCallBack(IAsyncResult ar)
        {
            try
            {
                Socket handler = (Socket)ar.AsyncState;
                int byteSent = handler.EndSend(ar);
                Console.WriteLine($"Poslato {byteSent} bajtova klijentu.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška pri slanju: {ex.Message}");
            }
        }
        }

       
}
