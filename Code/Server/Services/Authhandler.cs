using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using ChatTCP.Server.Data;
using ChatTCP.Server.Networking;

namespace ChatTCP.Server.Services
{
    // Xử lý lệnh LOGIN/REGISTER gửi lên từ Client.
    // Giao thức: REGISTER;username;password;displayname  hoặc  LOGIN;username;password
    // Trả về:    OK;userId    hoặc    FAIL;lý do
    public class AuthHandler
    {
        private UserRepository userRepository;
        private ClientManager clientManager;

        public AuthHandler(UserRepository userRepository, ClientManager clientManager)
        {
            this.userRepository = userRepository;
            this.clientManager = clientManager;
        }

        public void Handle(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                StreamWriter writer = new StreamWriter(stream, Encoding.UTF8);
                writer.AutoFlush = true;

                string line = reader.ReadLine();
                if (string.IsNullOrEmpty(line))
                {
                    client.Close();
                    return;
                }

                string[] parts = line.Split(';');
                string command = parts[0].Trim().ToUpper();

                if (command == "REGISTER" && parts.Length == 4)
                {
                    HandleRegister(client, writer, parts[1], parts[2], parts[3]);
                }
                else if (command == "LOGIN" && parts.Length == 3)
                {
                    HandleLogin(client, writer, parts[1], parts[2]);
                }
                else
                {
                    writer.WriteLine("FAIL;Lệnh không hợp lệ");
                    client.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi AuthHandler: " + ex.Message);
                client.Close();
            }
        }

        private void HandleRegister(TcpClient client, StreamWriter writer, string username, string password, string displayName)
        {
            if (userRepository.GetByUsername(username) != null)
            {
                writer.WriteLine("FAIL;Username đã tồn tại");
                client.Close();
                return;
            }

            int userId = userRepository.CreateUser(username, password, displayName);
            writer.WriteLine("OK;" + userId);

            ClientSession session = new ClientSession();
            session.TcpClient = client;
            session.UserId = userId;
            session.Username = username;
            session.DisplayName = displayName;

            clientManager.Add(session);
        }

        private void HandleLogin(TcpClient client, StreamWriter writer, string username, string password)
        {
            UserModel user = userRepository.GetByUsername(username);

            if (user == null || user.PasswordHash != password)
            {
                writer.WriteLine("FAIL;Sai tài khoản hoặc mật khẩu");
                client.Close();
                return;
            }

            writer.WriteLine("OK;" + user.UserId);

            ClientSession session = new ClientSession();
            session.TcpClient = client;
            session.UserId = user.UserId;
            session.Username = user.Username;
            session.DisplayName = user.DisplayName;

            clientManager.Add(session);
        }
    }
}