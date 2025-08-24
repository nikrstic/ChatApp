using ChatClient.Services;
using ChatClient.ViewModel;
using ChatClient.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static Emoji.Wpf.EmojiData;

namespace ChatClient.Views
{
    /// <summary>
    /// Interaction logic for Login.xaml
    /// </summary>
    public partial class Login : Window
    {
        //public EmojiViewModel EmojiVM { get; set; }
        public Login()
        {
            InitializeComponent();
        }
        

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var nickname = UsernameTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(nickname))
            {
                MessageBox.Show("Unesi nickname.");
                return;
            }
            var service = new TcpClientService(port: 3234);
            
            service.StartClient("127.0.0.1", nickname);
            
            var emojiVM = new EmojiViewModel();
            emojiVM.LoadEmojis();
            
            var vm = new ChatViewModel(service, nickname,emojiVM);
            
            
            var main = new MainWindow(service,nickname)
            {
                DataContext = vm
               

            };
            main.Show();
                
            Close();
        }
    }
}
