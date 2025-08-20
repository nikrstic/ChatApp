using ChatShared.Models;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ChatClient.Services
{
    public class TcpClientService
    {
        private Socket _client;
        private readonly int _port;
        private readonly StringBuilder _accumulator = new StringBuilder();

        public event Action<string> MessageReceived;
        public event Action Connected;
        public event Action Disconnected;

        private class ObjectState
        {
            public const int BufferSize = 4096;
            public byte[] Buffer = new byte[BufferSize];
        }

        public TcpClientService(int port = 3234)
        {
            _port = port;
        }
        public void StartClient(string host, string nickname)
        {
            try
            {
                // Uzmi IPv4 adresu
                var entry = Dns.GetHostEntry(host);
                var ip = entry.AddressList.First(a => a.AddressFamily == AddressFamily.InterNetwork);
                var endPoint = new IPEndPoint(ip, _port);

                _client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _client.BeginConnect(endPoint, ConnectionCallback, nickname); // prosledimo nickname u state
            }
            catch (Exception ex)
            {
                MessageReceived?.Invoke($"[ERROR]: {ex.Message}");
            }
        }

        private void ConnectionCallback(IAsyncResult ar)
        {
            try
            {
                string nickname = (string)ar.AsyncState;
                _client.EndConnect(ar);
                Connected?.Invoke();

                // Pošalji JOIN čim se povežeš
                var join = new Message { Type = "join", From = nickname, Text = "" };
                SendMessage(join);

                BeginReceive();
            }
            catch (Exception ex)
            {
                MessageReceived?.Invoke($"[ERROR]: {ex.Message}");
            }
        }

        private void BeginReceive()
        {
            try
            {
                var state = new ObjectState();
                _client.BeginReceive(state.Buffer, 0, ObjectState.BufferSize, SocketFlags.None, ReceiveCallback, state);
            }
            catch (Exception ex)
            {
                MessageReceived?.Invoke($"[ERROR]: {ex.Message}");
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            var state = (ObjectState)ar.AsyncState;

            try
            {
                int bytes = _client.EndReceive(ar);
                if (bytes <= 0)
                {
                    // Server zatvorio konekciju
                    Disconnected?.Invoke();
                    return;
                }

                _accumulator.Append(Encoding.UTF8.GetString(state.Buffer, 0, bytes));
                string data = _accumulator.ToString();

                int idx;
                while ((idx = data.IndexOf("<EOF>", StringComparison.Ordinal)) >= 0)
                {
                    string frame = data.Substring(0, idx);
                    data = data.Substring(idx + 5);

                    // pošalji UI-ju sirovi JSON (VM može po želji da deserializuje)
                    MessageReceived?.Invoke(frame);
                }

                _accumulator.Clear();
                _accumulator.Append(data);

                // nastavi da primaš
                BeginReceive();
            }
            catch (ObjectDisposedException)
            {
                // socket zatvoren – ignoriši
            }
            catch (Exception ex)
            {
                MessageReceived?.Invoke($"[ERROR]: {ex.Message}");
                Disconnected?.Invoke();
            }
        }

        public void SendMessage(Message msg)
        {
            string json = JsonSerializer.Serialize(msg) + "<EOF>";
            SendRaw(json);
        }

        public void SendRaw(string payload)
        {
            if (_client == null || !_client.Connected) return;

            byte[] data = Encoding.UTF8.GetBytes(payload);
            _client.BeginSend(data, 0, data.Length, SocketFlags.None, SendCallback, null);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try { _client.EndSend(ar); }
            catch { /* ignore */ }
        }

        public void Disconnect()
        {
            try { _client?.Shutdown(SocketShutdown.Both); } catch { }
            try { _client?.Close(); } catch { }
            Disconnected?.Invoke();
        }
    }
}
