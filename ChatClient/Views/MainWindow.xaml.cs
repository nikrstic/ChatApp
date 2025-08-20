using System.Windows;
using System.Windows.Input;

namespace ChatClient.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnKeyDownSend(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter &&
                (DataContext as dynamic)?.SendCommand?.CanExecute(null) == true)
            {
                (DataContext as dynamic)?.SendCommand?.Execute(null);
            }
        }
    }
}
