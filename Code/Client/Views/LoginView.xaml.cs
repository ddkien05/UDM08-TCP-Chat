using System;
using System.Windows;
using System.Windows.Controls;
using Client.Services;

namespace Client.Views
{
    public partial class LoginView : UserControl
    {
        private readonly ClientSocketService _socketService;

        public event Action? LoginSucceeded;

        public LoginView()
        {
            InitializeComponent();

            _socketService = new ClientSocketService();
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

            ErrorText.Visibility = Visibility.Collapsed;

            try
            {
                // Tạm khóa nút để tránh người dùng bấm nhiều lần
                if (sender is Button loginButton)
                {
                    loginButton.IsEnabled = false;
                    loginButton.Content = "Đang kết nối...";
                }

                bool connected = await _socketService.ConnectAsync(
                    "127.0.0.1",
                    9000
                );

                if (!connected)
                {
                    ShowError("Không thể kết nối tới Server.");

                    return;
                }

                // QUAN TRỌNG:
                // Hiện server nhánh dev mới chỉ nhận TCP connection.
                // Username/password CHƯA được server xác thực.
                //
                // Sau khi server có AUTH_REQ / AUTH_RES,
                // phần xác thực thật sẽ được thêm tại đây.

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

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}

