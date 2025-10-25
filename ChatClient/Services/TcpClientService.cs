using ChatClient.Services;
using ChatShared.Models;
using System;
using System.Diagnostics;
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
                var entry = Dns.GetHostEntry(host);
                var ip = entry.AddressList.First(a => a.AddressFamily == AddressFamily.InterNetwork);
                var endPoint = new IPEndPoint(ip, _port);

                Debug.WriteLine($"[CLIENT] Connecting to {ip}:{_port} as '{nickname}'...");

                _client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _client.BeginConnect(endPoint, ConnectionCallback, nickname);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CLIENT ERROR] StartClient: {ex.Message}");
                MessageReceived?.Invoke($"[ERROR]: {ex.Message}");
            }
        }

        private void ConnectionCallback(IAsyncResult ar)
        {
            try
            {
                _client.EndConnect(ar);
                string nickname = (string)ar.AsyncState;
                Debug.WriteLine($"[CLIENT] Connected to server as '{nickname}'");

                BeginReceive();

                Connected?.Invoke();

                var join = new Message { Type = "join", From = nickname };
                Debug.WriteLine($"[CLIENT] Sending join message: {JsonSerializer.Serialize(join)}");
                SendMessage(join);

                
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CLIENT ERROR] ConnectionCallback: {ex.Message}");
                MessageReceived?.Invoke($"[ERROR]: {ex.Message}");
            }
        }

        private void BeginReceive()
        {
            try
            {
                var state = new ObjectState();
                Debug.WriteLine("[CLIENT] BeginReceive()");
                _client.BeginReceive(state.Buffer, 0, ObjectState.BufferSize, SocketFlags.None, ReceiveCallback, state);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CLIENT ERROR] BeginReceive: {ex.Message}");
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
                    Debug.WriteLine("[CLIENT] Server closed connection.");
                    Disconnected?.Invoke();
                    return;
                }

                string chunk = Encoding.UTF8.GetString(state.Buffer, 0, bytes);
                Debug.WriteLine($"\n[CLIENT] Received {bytes} bytes: {chunk}");

                _accumulator.Append(chunk);
                string data = _accumulator.ToString();

                int idx;
                while ((idx = data.IndexOf("<EOF>", StringComparison.Ordinal)) >= 0)
                {
                    string frame = data.Substring(0, idx);
                    data = data.Substring(idx + 5);

                    Debug.WriteLine($"[CLIENT] Extracted frame:\n{frame}\n-------------------------");
                    MessageReceived?.Invoke(frame);
                }

                _accumulator.Clear();
                if (!string.IsNullOrEmpty(data))
                {
                    _accumulator.Append(data);
                    Debug.WriteLine($"[CLIENT] Remaining partial data in accumulator: {data}");
                }
                else
                {
                    Debug.WriteLine("[CLIENT] No partial data remaining.");
                }

                Debug.WriteLine("[CLIENT] Waiting for next data...\n");
                _client.BeginReceive(state.Buffer, 0, ObjectState.BufferSize, SocketFlags.None, ReceiveCallback, state);
            }
            catch (ObjectDisposedException)
            {
                Debug.WriteLine("[CLIENT] Socket closed.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CLIENT ERROR] ReceiveCallback: {ex.Message}");
                MessageReceived?.Invoke($"[ERROR]: {ex.Message}");
                Disconnected?.Invoke();
            }
        }

        public void SendMessage(Message msg)
        {
            string json = JsonSerializer.Serialize(msg) + "<EOF>";
            Debug.WriteLine($"[CLIENT] Sending message: {json}");
            SendRaw(json);
        }

        public void SendRaw(string payload)
        {
            if (_client == null || !_client.Connected)
            {
                Debug.WriteLine("[CLIENT] Attempted to send, but not connected.");
                return;
            }

            byte[] data = Encoding.UTF8.GetBytes(payload);
            _client.BeginSend(data, 0, data.Length, SocketFlags.None, SendCallback, null);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                _client.EndSend(ar);
                Debug.WriteLine("[CLIENT] Message sent successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CLIENT ERROR] SendCallback: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            try
            {
                Debug.WriteLine("[CLIENT] Disconnecting...");
                _client?.Shutdown(SocketShutdown.Both);
            }
            catch { }

            try { _client?.Close(); } catch { }
            Disconnected?.Invoke();
        }
    }
  
    }
