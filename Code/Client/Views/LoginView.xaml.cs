using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Xps;
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
            _socketService = new ClientSocketService(Dispatcher);
            _socketService.OnError += ShowError;
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
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

            try
            {
                // Tạm khóa nút để tránh người dùng bấm nhiều lần
                if (sender is Button loginButton)
                {
                    loginButton.IsEnabled = false;
                    loginButton.Content = "Đang kết nối...";
                }

                bool loginSuccess = await _socketService.LoginAsync(serverIp, port, username, password);

                if (!loginSuccess)
                {
                    // Lỗi đã được hiển thị qua sự kiện OnError của ClientSocketService,
                    // Nhưng ta có thể để LoginSucceeded không được gọi.
                    return;
                }

                LoginSucceeded?.Invoke();
            }
            catch (Exception ex)
            {
                ShowError("Lỗi kết nối: " + ex.Message);
            }
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
        }
    }
}

