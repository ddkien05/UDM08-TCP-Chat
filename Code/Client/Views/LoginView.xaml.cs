using System;

using System.Windows;
using System.Windows.Controls;

using ChatTCP.Client.Networking;
using ChatTCP.Common.Models;

namespace Client.Views
{
    public partial class LoginView :
        UserControl
    {
        private readonly ClientSocketService
            _socketService;

        public event Action<
            ClientSocketService,
            LoginResponseData>?
            LoginSucceeded;

        public LoginView()
        {
            InitializeComponent();

            _socketService =
                new ClientSocketService(
                    Dispatcher);

            _socketService.OnLoginResponse +=
                SocketService_OnLoginResponse;

            _socketService.OnError +=
                SocketService_OnError;
        }

        private async void LoginButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ErrorText.Visibility =
                Visibility.Collapsed;

            string username =
                UsernameTextBox.Text.Trim();

            string password =
                PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(
                    username)
                ||
                string.IsNullOrWhiteSpace(
                    password))
            {
                ShowError(
                    "Vui lòng nhập tài khoản và mật khẩu.");

                return;
            }

            bool connected =
                await _socketService.ConnectAsync(
                    "127.0.0.1",
                    8888);

            if (!connected)
            {
                return;
            }

            await _socketService.LoginAsync(
                username,
                password);
        }

        private void SocketService_OnLoginResponse(
            Packet<LoginResponseData> packet)
        {
            if (!packet.Data.Success)
            {
                ShowError(
                    packet.Data.Message);

                return;
            }

            _socketService.OnLoginResponse -=
                SocketService_OnLoginResponse;

            _socketService.OnError -=
                SocketService_OnError;

            LoginSucceeded?.Invoke(
                _socketService,
                packet.Data);
        }

        private void SocketService_OnError(
            string message)
        {
            ShowError(
                message);
        }

        private void ShowError(
            string message)
        {
            ErrorText.Text =
                message;

            ErrorText.Visibility =
                Visibility.Visible;
        }
    }
}