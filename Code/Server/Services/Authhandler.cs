using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using ChatTCP.Server.Data;
using ChatTCP.Server.Networking;

namespace ChatTCP.Server.Services
{
    public class AuthHandler
    {
        private readonly IUserRepository _userRepository;
        private readonly ClientManager _clientManager;

        public AuthHandler(IUserRepository userRepository, ClientManager clientManager)
        {
            _userRepository = userRepository;
            _clientManager = clientManager;
        }

        /// Đọc 1 dòng lệnh LOGIN/REGISTER từ client và xử lý. Bọc try-catch để 1 client lỗi không làm chết server.
        public void Handle(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                string line = reader.ReadLine(); // đứng chờ tới khi client gửi lệnh
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
                // lưới an toàn cuối cùng — không để lỗi từ 1 client làm sập cả server
                Console.WriteLine("[AuthHandler] Lỗi không mong muốn: " + ex.Message);
                client.Close();
            }
        }

        ///Xử lý đăng ký: kiểm tra trùng Username rồi gọi UserRepository tạo user mới.
        private void HandleRegister(TcpClient client, StreamWriter writer,
                                     string username, string password, string displayName)
        {
            try
            {
                if (_userRepository.GetByUsername(username) != null)
                {
                    writer.WriteLine("FAIL;Username đã tồn tại");
                    client.Close();
                    return;
                }

                // Lưu ý: đang lưu password thô cho đơn giản, chưa băm (hash).
                int userId = _userRepository.CreateUser(username, password, displayName);
                writer.WriteLine("OK;" + userId);

                _clientManager.Add(new ClientSession
                {
                    TcpClient = client,
                    UserId = userId,
                    Username = username,
                    DisplayName = displayName
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("[AuthHandler] Lỗi khi Register: " + ex.Message);
                writer.WriteLine("FAIL;Lỗi hệ thống, thử lại sau");
                client.Close();
            }
        }

        ///Xử lý đăng nhập: kiểm tra Username/Password qua UserRepository
        private void HandleLogin(TcpClient client, StreamWriter writer, string username, string password)
        {
            try
            {
                UserModel user = _userRepository.GetByUsername(username);

                if (user == null || user.PasswordHash != password)
                {
                    writer.WriteLine("FAIL;Sai tài khoản hoặc mật khẩu");
                    client.Close();
                    return;
                }

                writer.WriteLine("OK;" + user.UserId);

                _clientManager.Add(new ClientSession
                {
                    TcpClient = client,
                    UserId = user.UserId,
                    Username = user.Username,
                    DisplayName = user.DisplayName
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("[AuthHandler] Lỗi khi Login: " + ex.Message);
                writer.WriteLine("FAIL;Lỗi hệ thống, thử lại sau");
                client.Close();
            }
        }
    }
}