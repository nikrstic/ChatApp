using ChatClient.Services;
using ChatClient.ViewModel;
using ChatClient.ViewModels;
using ChatShared.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Windows.Media.Protection.PlayReady;
using static Emoji.Wpf.EmojiData;


namespace ChatClient.Views
{
    public partial class MainWindow : Window
    {
        private TcpClientService _client;
        private string _nickname;


        public MainWindow(TcpClientService client, string nickname)
        {
            
            InitializeComponent();
            _client = client;
            _nickname = nickname;

            
            

        }
        // current message mi je null dok se ne klikne negde sa strane
        private void OnKeyDownSend(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender is TextBox tb)
                    tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

                var vm = DataContext as ChatViewModel;
                if (vm?.SendCommand?.CanExecute(null) == true)
                    vm.SendCommand.Execute(null);
            }
        }
        private void OnlineUsersList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox lb && lb.SelectedItem is string selectedUser)
            {
                var vm = (ChatViewModel)DataContext;

                if (selectedUser != vm.Nickname)
                {
                    vm.PrivateVM.OpenPrivateChatCommand.Execute(selectedUser);
                }
            }
        }



    }
}
