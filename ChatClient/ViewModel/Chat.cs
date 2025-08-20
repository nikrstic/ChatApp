using ChatClient.Commands;
using ChatClient.Services;
using ChatShared.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace ChatClient.ViewModels
{
    public class ChatViewModel : INotifyPropertyChanged
    {
        private readonly TcpClientService _client;
        private readonly string _nickname;

        public ObservableCollection<string> Messages { get; } = new ObservableCollection<string>();

        private string _currentMessage;
        public string CurrentMessage
        {
            get => _currentMessage;
            set { _currentMessage = value; OnPropertyChanged(nameof(CurrentMessage)); }
        }

        public ICommand SendCommand { get; }

        public ChatViewModel(TcpClientService client, string nickname)
        {
            _client = client;
            _nickname = nickname;

            _client.MessageReceived += OnMessageFromServer;

            SendCommand = new RelayCommand(SendMessage);
        }

        private void OnMessageFromServer(string payload)
        {
            // payload je JSON jedne poruke (bez <EOF>)
            // možeš ili da prikažeš sirovo, ili da ga parsiraš:
            try
            {
                var msg = JsonSerializer.Deserialize<Message>(payload);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (msg != null)
                    {
                        var who = string.IsNullOrWhiteSpace(msg.From) ? "System" : msg.From;
                        Messages.Add($"[{who}]: {msg.Text}");
                    }
                    else
                    {
                        Messages.Add(payload); // fallback
                    }
                });
            }
            catch
            {
                Application.Current.Dispatcher.Invoke(() => Messages.Add(payload));
            }
        }

        private void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(CurrentMessage)) return;

            var msg = new Message
            {
                Type = "message",
                From = _nickname,
                Text = CurrentMessage
            };

            _client.SendMessage(msg);

            // opcionalno prikaži i lokalno
            Messages.Add($"[Me]: {CurrentMessage}");
            CurrentMessage = string.Empty;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
