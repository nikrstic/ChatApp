using ChatClient.Commands;
using ChatClient.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using static ChatClient.Models.EmojiModel;

namespace ChatClient.ViewModel
{


    public class EmojiViewModel :INotifyPropertyChanged
    {
       

        private bool _isEmojiPanelOpen = false;
        

        public bool IsEmojiPanelOpen
        {
            get => _isEmojiPanelOpen;
            set
            {
                _isEmojiPanelOpen = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEmojiPanelOpen)));
            }
        }
        public ObservableCollection<EmojiGroup> EmojiGroups { get; } = new();

        public event Action<string>? EmojiSelected;
        public event PropertyChangedEventHandler? PropertyChanged;

        // ovo da zovem u .xaml.cs
        public async void LoadEmojis(){

            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Resources", "Emoji-test.txt");

            await Task.Run(() =>
            {
                var groups = EmojiParsing.ParseEmojiFile(path);

                App.Current.Dispatcher.Invoke(() =>
                {
                    EmojiGroups.Clear();
                    foreach (var group in groups)
                        EmojiGroups.Add(group);
                });
                
            });
        }

        // ovo je onaj relay kommand a imam u .xaml binding {IsEmojipanelOpen
        public ICommand OpenEmojiPanelCommand => new RelayCommand(() => IsEmojiPanelOpen = !IsEmojiPanelOpen);
        public ICommand InsertEmojiCommand => new RelayCommand<string>(emoji =>
        {
            EmojiSelected?.Invoke(emoji);
            IsEmojiPanelOpen = false;
        });
       

    }
}
