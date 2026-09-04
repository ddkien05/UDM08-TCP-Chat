using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
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
                var res = await SendRegisterCommandAsync(username, password, displayName);
                if (res.StartsWith("OK;"))
                {
                    RegistrationSucceeded?.Invoke();
                }
                else
                {
                    ShowError(res);
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
        /// Send a plain-text REGISTER command to AuthHandler (port 8888).
        /// Format: REGISTER;username;password;displayName
        /// Returns server response line.
        /// </summary>
        private Task<string> SendRegisterCommandAsync(string username, string password, string displayName)
        {
            return Task.Run(() =>
            {
                using var client = new TcpClient();
                client.Connect("127.0.0.1", 8888);
                using var stream = client.GetStream();
                using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                using var reader = new StreamReader(stream, Encoding.UTF8);

                string cmd = $"REGISTER;{username};{password};{displayName}";
                writer.WriteLine(cmd);
                // Read single line response
                string? resp = reader.ReadLine();
                return resp ?? "FAIL;No response";
            });
        }

        private void PhoneNumberTextBox_TextChanged()
        {

        }
    }
}
