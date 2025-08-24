using ChatServer.Models;

using ChatShared;
using ChatShared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace ChatServer.Services
{
    public class Server
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
        // kad stigne nesto od klijenta
        public static void ReadCallBack(IAsyncResult ar)
        {
            // podaci o klijentu
            ObjectState state = (ObjectState)ar.AsyncState;
            Socket handler = state.wSocket;

            try
            {
                int bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0)
                {
                    state.sb.Append(Encoding.UTF8.GetString(state.buffer, 0, bytesRead));
                    string content = state.sb.ToString();

                    int idx = 0;

                    while ((idx = content.IndexOf("<EOF>", StringComparison.Ordinal)) >= 0)
                    {
                        string frame = content.Substring(0, idx);
                        content = content.Substring(idx + 5);


                        try
                        {
                            var msg = JsonSerializer.Deserialize<Message>(frame);


                                //string cleanMessage = content.Replace("<EOF>", "").Trim();

                                if (!klijenti.ContainsKey(handler) && msg.Type == "join")
                                {
                                    string nickname = msg.From;
                                    var info = new ClientInfo { Socket = handler, ID = ID++, Nickname = nickname };

                                    lock (klijenti)
                                        klijenti[handler] = info;

                                    // saljemo mu inicijalne podatke
                                    var init = new
                                    {
                                        Type = "init",
                                        Users = klijenti.Values.Select(c => c.Nickname).ToList(),
                                        Welcome = $"Dobrodošao, {nickname}!"
                                    };
                                    Send(handler, JsonSerializer.Serialize(init) + "<EOF>");

                                    // ostalima kazemo da je dosao novi
                                    var joined = new { Type = "user_joined", User = nickname };
                                    Broadcast(JsonSerializer.Serialize(joined) + "<EOF>", handler);
                                }
                                else if (msg.Type == "message")
                                {
                                    var sender = klijenti[handler];
                                    var broadcastMsg = new Message
                                    {
                                        Type = "message",
                                        From = sender.Nickname,
                                        Text = msg.Text
                                    };
                                    Broadcast(JsonSerializer.Serialize(broadcastMsg) + "<EOF>", handler);
                                }
                                else if (msg.Type == "privateMessage")
                                {
                                    var sender = klijenti[handler];
                                    var pm = new Message
                                    {
                                        Type = "privateMessage",
                                        From = sender.Nickname,
                                        To = msg.To,
                                        Text = msg.Text
                                    };

                                    string pmJson = JsonSerializer.Serialize(pm) + "<EOF>";
                                    var target = klijenti.Values.FirstOrDefault(c => c.Nickname == msg.To);

                                    if (target != null)
                                        Send(target.Socket, pmJson);

                                    // i sebi salje kopiju
                                    Send(handler, pmJson);
                                }
                        }
                        catch (JsonException)
                        {
                            content = frame + content;
                            break;
                        }
                        
                        catch (Exception ex)
                        {
                            Console.WriteLine("Greska: " + ex.Message);
                        }
                    }
                    state.sb.Clear();
                    state.sb.Append(content);

                    handler.BeginReceive(state.buffer, 0, ObjectState.bufferSize, 0, new AsyncCallback(ReadCallBack), state);
                }
            }
            catch
            {
                Disconnect(handler);
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
                    
                    ChatShared.Models.ActiveClients.Remove(user.Nickname);

                    Message msg = new Message
                    {
                        Type = "system",
                        Text = $"{user.Nickname} je napustio chat. "


                    };
                    var leftMsg = new
                    {
                        Type = "user_left",
                        User = user.Nickname
                    };
                    Broadcast(JsonSerializer.Serialize(leftMsg)+"<EOF>", handler);  
                    //Broadcast(JsonSerializer.Serialize(msg) + "<EOF>", handler);

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
            byte[] data = Encoding.UTF8.GetBytes(message);

            lock (klijenti)
            {
                foreach (var klijent in klijenti)
                {
                    if (klijent.Key != sender)
                    {
                        //asinhroni poziv bez blokiranja niti 
                        klijent.Key.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallBack), klijent.Key);
                    }
                }
            }
        }

        private static void Send(Socket handler, string content)
        {
            byte[] byteData = Encoding.UTF8.GetBytes(content);
            handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallBack), handler);
        }
        // kaze uspesno je poslata ili hvata gresku da nije
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
