using ChatTCP.Common.Models;
using ChatTCP.Common.Protocol;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ChatTCP.Client.Views
{
    public partial class RegisterView : UserControl
    {
        public event Action? RegistrationSucceeded;
        public event Action? BackRequested;

        public RegisterView()
        {
            InitializeComponent();
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text?.Trim() ?? string.Empty;
            string password = PasswordBox.Password ?? string.Empty;
            string displayName = DisplayNameTextBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username)) { ShowError("Username is required."); return; }
            if (string.IsNullOrWhiteSpace(password)) { ShowError("Password is required."); return; }
            if (string.IsNullOrWhiteSpace(displayName)) { ShowError("Display name is required."); return; }

            RegisterButton.IsEnabled = false;
            ErrorText.Visibility = Visibility.Collapsed;

            try
            {
                var (ok, message) = await SendRegisterCommandAsync(username, password, displayName);
                if (ok)
                {
                    RegistrationSucceeded?.Invoke();
                }
                else
                {
                    ShowError(message);
                }
            }
            catch (Exception ex)
            {
                ShowError("Error: " + ex.Message);
            }
            finally
            {
                RegisterButton.IsEnabled = true;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke();
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Gửi lệnh REGISTER dạng plain-text tới AuthHandler (cổng 8888).
        /// Định dạng: REGISTER;username;password;displayName
        /// Trả về dòng phản hồi từ server.
        /// </summary>
        private async Task<(bool ok, string message)> SendRegisterCommandAsync(
    string username, string password, string displayName)
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", 8888);
            using var stream = client.GetStream();

            var packet = new Packet<AuthRequestData>
            {
                Type = "REGISTER",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new AuthRequestData
                {
                    Username = username,
                    Password = password,
                    DisplayName = displayName
                }
            };
            await MessageProtocol.SendPacketAsync(stream, packet);

            string? raw = await MessageProtocol.ReceiveRawJsonAsync(stream);
            if (string.IsNullOrEmpty(raw))
                return (false, "Không nhận được phản hồi từ Server.");

            var res = JsonSerializer.Deserialize<Packet<JsonElement>>(raw);
            if (res == null || res.Type != "AUTH_RESPONSE")
                return (false, "Phản hồi không hợp lệ từ Server.");

            int code = res.Data.GetProperty("code").GetInt32();
            string msg = res.Data.GetProperty("message").GetString() ?? "Lỗi không xác định";
            return (code == 200, msg);
        }

        private void PhoneNumberTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}
