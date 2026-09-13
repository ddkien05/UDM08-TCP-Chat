using System;
using System.Windows;
using System.Windows.Controls;
using ChatTCP.Client.Networking;

namespace Client.Views
{
    public partial class LoginView :
        UserControl
    {
        private readonly ClientSocketService
            _socketService;

        public event Action<ClientSocketService>?
            LoginSucceeded;

        public LoginView()
        {
            InitializeComponent();

            _socketService =
                new ClientSocketService(
                    Dispatcher);
        }

        private async void LoginButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                bool connected =
                    await _socketService
                        .ConnectAsync(
                            "127.0.0.1",
                            8888);

                if (connected)
                {
                    MessageBox.Show(
                        "Kết nối Server thành công.");

                    /*
                     * Truyền socket hiện tại sang màn sau.
                     *
                     * Không tạo ClientSocketService mới,
                     * nếu không sẽ mất connection/event
                     * USER_STATUS_NOTIFY.
                     */
                    LoginSucceeded?.Invoke(
                        _socketService);
                }
                else
                {
                    MessageBox.Show(
                        "Không thể kết nối Server.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message);
            }
        }
    }
}