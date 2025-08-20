using ChatClient.Services;
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

namespace ChatClient.Views
{
    /// <summary>
    /// Interaction logic for Login.xaml
    /// </summary>
    public partial class Login : Window
    {
        public Login()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var nickname = UsernameTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(nickname))
            {
                MessageBox.Show("Unesi nickname.");
                return;
            }
            var service = new TcpClientService(port: 3234);
            
            service.StartClient("127.0.0.1", nickname);
            var vm = new ChatViewModel(service, nickname);

            var main = new MainWindow
            {
                DataContext = vm
            };
            main.Show();

            Close();
        }
    }
}
