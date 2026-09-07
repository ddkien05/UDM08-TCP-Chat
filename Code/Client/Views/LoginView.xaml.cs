using System;
using System.Windows;
using System.Windows.Controls;
using ChatTCP.Client.Networking;

namespace Client.Views
{
    public partial class LoginView : UserControl
    {
        private readonly ClientSocketService _socketService;

        public event Action? LoginSucceeded;

        public LoginView()
        {
            InitializeComponent();

            _socketService =
                new ClientSocketService(Dispatcher);
        }

        private async void LoginButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                bool connected =
                    await _socketService.ConnectAsync(
                        "127.0.0.1",
                        8888);

                if (connected)
                {
                    MessageBox.Show(
                        "Kết nối Server thành công.");

                    LoginSucceeded?.Invoke();
                }
                else
                {
                    MessageBox.Show(
                        "Không thể kết nối Server.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}