using ChatClient.Commands;
using ChatClient.Models;
using ChatClient.Services;
using ChatClient.ViewModels;
using ChatShared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Windows.Media.Protection.PlayReady;
using static Emoji.Wpf.EmojiData;

namespace ChatClient.ViewModel
{
    public class PrivateChatViewModel : INotifyPropertyChanged

    {
        private TcpClientService _client;
        private string _nickname;
        public EmojiViewModel EmojiVM { get; }
        public ObservableCollection<string> OnlineUsers { get; } = new ObservableCollection<string>();

        public ObservableCollection<PrivateChat> privateChats { get; } = new ObservableCollection<PrivateChat>();

        private PrivateChat? _activeChat;

        public event Action<PrivateChat>? ChatOpened;


        public PrivateChat? ActiveChat
        {
            get => _activeChat;
            set
            {
                _activeChat = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveChat)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChatTitle)));
            }

        }
        private string _currentMessage = string.Empty;
        public string CurrentMessage
        {
            get => _currentMessage;
            set { _currentMessage = value;PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentMessage))); } 
        }

        public ICommand OpenPrivateChatCommand => new RelayCommand<string>(OpenPrivateChat);
        public ICommand SendCommand { get; }


        public PrivateChatViewModel(TcpClientService client, string nickname)
        {
            _client = client;
            _nickname = nickname;
            EmojiVM = new EmojiViewModel();
            EmojiVM.LoadEmojis();


            //_client.MessageReceived += OnMessageFromServer;
            EmojiVM.EmojiSelected += emoji => CurrentMessage += emoji;


          
            SendCommand = new RelayCommand(SendPrivateMessage);
        }

       

        
       

        

        private void OpenPrivateChat(string otherUser)
        {
            if (string.IsNullOrWhiteSpace(otherUser) || otherUser == _nickname) return;

            var chat = privateChats.FirstOrDefault(c =>
            (c.User1 == _nickname && c.User2 == otherUser) || (c.User1 == otherUser && c.User2 == _nickname));

            if (chat == null)
            {
                chat=new PrivateChat { User1 = _nickname, User2 = otherUser };
                privateChats.Add(chat);

                ChatOpened?.Invoke(chat);
            }
            ActiveChat = chat; 
        }

        private void SendPrivateMessage()
        {
            if (ActiveChat == null || string.IsNullOrWhiteSpace(CurrentMessage)) return;

            var msg = new Message
            {
                Type = "privateMessage",
                From = _nickname,
                To = ActiveChat.User1 == _nickname ? ActiveChat.User2 : ActiveChat.User1,
                Text = CurrentMessage
            };

            _client.SendMessage(msg);

            //ActiveChat.Messages.Add($"Me: {CurrentMessage}");
            CurrentMessage = string.Empty;

        }
      

        public string ActiveChatTitle{

            get
            {
                if (ActiveChat == null)
                    return "Privatni chat";
                return $"Chat sa {(ActiveChat.User1 == _nickname ? ActiveChat.User2 : ActiveChat.User1)}";
            }

        }

        public string ChatTitle => ActiveChat == null ? "Privatni chat" : $"Chat sa {(ActiveChat.User1 == _nickname ? ActiveChat.User2 : ActiveChat.User1)}";
      
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
