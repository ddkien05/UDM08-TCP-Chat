using System;
using System.Windows;
using System.Windows.Controls;
<<<<<<< HEAD
using System.Windows.Xps;
=======
>>>>>>> f87406ee404b767d41992a84972afdc0635611fc
using ChatTCP.Client.Networking;

namespace ChatTCP.Client.Views
{
    public partial class LoginView : UserControl
    {
        private readonly ClientSocketService _socketService;

        public event Action? LoginSucceeded;
        public event Action? RegisterRequested;

        public LoginView()
        {
            InitializeComponent();
<<<<<<< HEAD
            _socketService = new ClientSocketService(Dispatcher);
            _socketService.OnError += ShowError;
=======

            _socketService =
                new ClientSocketService(Dispatcher);
>>>>>>> f87406ee404b767d41992a84972afdc0635611fc
        }

        private async void LoginButton_Click(
            object sender,
            RoutedEventArgs e)
        {
<<<<<<< HEAD
            string username = UsernameTextBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username))
            {
                ShowError("Vui lòng nhập tên đăng nhập.");
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("Vui lòng nhập mật khẩu.");
                return;
            }

            string serverIp = ServerIpTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(serverIp))
            {
                serverIp = "127.0.0.1";
            }

            if (!int.TryParse(PortTextBox.Text.Trim(), out int port))
            {
                ShowError("Cổng (Port) không hợp lệ. Vui lòng nhập số.");
                return;
            }

            ErrorText.Visibility = Visibility.Collapsed;

=======
>>>>>>> f87406ee404b767d41992a84972afdc0635611fc
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
<<<<<<< HEAD

                bool loginSuccess = await _socketService.LoginAsync(serverIp, port, username, password);

                if (!loginSuccess)
                {
                    // Lỗi đã được hiển thị qua sự kiện OnError của ClientSocketService,
                    // Nhưng ta có thể để LoginSucceeded không được gọi.
                    return;
                }

                LoginSucceeded?.Invoke();
=======
                else
                {
                    MessageBox.Show(
                        "Không thể kết nối Server.");
                }
>>>>>>> f87406ee404b767d41992a84972afdc0635611fc
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
<<<<<<< HEAD
            finally
            {
                if (sender is Button loginButton)
                {
                    loginButton.IsEnabled = true;
                    loginButton.Content = "Đăng nhập";
                }
            }
        }

        private void RegisterNavButton_Click(object sender, RoutedEventArgs e)
        {
            RegisterRequested?.Invoke();
        }


        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
=======
>>>>>>> f87406ee404b767d41992a84972afdc0635611fc
        }
    }
}