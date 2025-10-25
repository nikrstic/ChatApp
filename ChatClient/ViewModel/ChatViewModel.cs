using ChatClient.Commands;
using ChatClient.Models;
using ChatClient.Services;
using ChatClient.ViewModel;
using ChatClient.Views;
using ChatShared.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace ChatClient.ViewModels
{
    public class ChatViewModel : INotifyPropertyChanged
    {
        private readonly TcpClientService _client;
        private readonly string _nickname;
        public EmojiViewModel EmojiVM { get; }



       
     
        public TcpClientService Client => _client;
        public string Nickname => _nickname;

        public PrivateChatViewModel PrivateVM { get; }

        // kolekcija koja direktno radi sa ui update
        public ObservableCollection<string> OnlineUsers { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> Messages { get; } = new ObservableCollection<string>();


        private Dictionary<string, PrivateChatWindow> openPrivateChats = new();

        private string _currentMessage;
        public string CurrentMessage
        {
            get => _currentMessage;
            set { _currentMessage = value; OnPropertyChanged(nameof(CurrentMessage)); }
        }

        public ICommand SendCommand { get; }

        public ChatViewModel(TcpClientService client, string nickname, EmojiViewModel emojiVM)
        {
            _client = client;
            _nickname = nickname;

            _client.MessageReceived += OnMessageFromServer;

            

            EmojiVM = emojiVM;
           
            EmojiVM.EmojiSelected += emoji =>
            {
                CurrentMessage += emoji;
            };
            SendCommand = new RelayCommand(SendMessage);

            PrivateVM = new PrivateChatViewModel(_client, _nickname);
            PrivateVM.ChatOpened += OnPrivateChatOpened;
        }
        private void OnPrivateChatOpened(PrivateChat chat)
        {
            string otherUser = chat.User1 == _nickname ? chat.User2 : chat.User1;

            if (openPrivateChats.ContainsKey(otherUser))
            {
                openPrivateChats[otherUser].Activate();
                return;
            }

            //poseban ViewModel za svakog
            var privateVm = new PrivateChatViewModel(_client, _nickname)
            {
                ActiveChat = chat
            };

            var privateWindow = new PrivateChatWindow
            {
                DataContext = privateVm,
                Owner = System.Windows.Application.Current.MainWindow
            };

            openPrivateChats[otherUser] = privateWindow;

            privateWindow.Closed += (s, e) =>
            {
                openPrivateChats.Remove(otherUser);
            };

            privateWindow.Show();
        }

        private void OnMessageFromServer(string payload)
        {
            // payload je JSON jedne poruke bez <EOF>
            try
            {
                var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                string type = root.GetProperty("Type").GetString();

                var msg = JsonSerializer.Deserialize<Message>(payload);

                Debug.WriteLine($"[UI TEST] Received payload: {payload}");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    switch (type)
                    {
                        //case "user_list":
                        //    OnlineUsers.Clear();
                        //    foreach (var u in root.GetProperty("Users").EnumerateArray())
                        //        OnlineUsers.Add(u.GetString());
                        //    break;

                        case "user_joined":
                            string joined = root.GetProperty("User").GetString();
                            if (!OnlineUsers.Contains(joined))
                                OnlineUsers.Add(joined);
                            Messages.Add($"[SERVER]: {joined} se pridruzio.");
                            break;

                        case "user_left":
                            string left = root.GetProperty("User").GetString();
                            OnlineUsers.Remove(left);
                            Messages.Add($"[SERVER]: {left} je napustio chat.");
                            break;

                        case "message":
                            string who = root.GetProperty("From").GetString();
                            string text = root.GetProperty("Text").GetString();
                            Messages.Add($"[{who}]: {text}");
                            break;

                        case "system":
                            Messages.Add($"[SERVER]: {root.GetProperty("Text").GetString()}");
                            break;
                        case "init":
                            string Ujoined = root.GetProperty("Welcome").GetString();
                            Messages.Add($"[SERVER]: {Ujoined}");
                            OnlineUsers.Clear();
                            foreach (var u in root.GetProperty("Users").EnumerateArray())
                                OnlineUsers.Add(u.GetString());
                            break;
                        case "privateMessage":
                            //PrivateChatViewModel VM = new PrivateChatViewModel(Client, Nickname, EmojiVM);
                            string from = root.GetProperty("From").GetString();
                            string to = root.GetProperty("To").GetString();
                            string textPm = root.GetProperty("Text").GetString();

                            string otherUser = from == _nickname ? to : from;

                            // ako vec postoji prozor za tog usera
                            if (openPrivateChats.TryGetValue(otherUser, out var window))
                            {
                                var vm = (PrivateChatViewModel)window.DataContext;
                                vm.ActiveChat.Messages.Add($"{from}: {textPm}");
                            }
                            else
                            {
                                // napravi novi chat
                                var chat = new PrivateChat { User1 = _nickname, User2 = otherUser };
                                chat.Messages.Add($"{from}: {textPm}");
                                OnPrivateChatOpened(chat);
                            }
                            

                            break;
                        default:
                            Messages.Add(payload); // fallback
                            break;
                    }
                });
            }

            catch 
            { 
                //Application.Current.Dispatcher.Invoke(() => Messages.Add(payload));
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

            // prikazi i lokalno
            Messages.Add($"[Me]: {CurrentMessage}");
            CurrentMessage = string.Empty;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>  
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));



        


    }

}
