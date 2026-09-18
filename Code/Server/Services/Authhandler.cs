using System;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using ChatTCP.Common.Models;
using ChatTCP.Common.Protocol;
using ChatTCP.Server.Data;
using ChatTCP.Server.Networking;

namespace ChatTCP.Server.Services
{
   
    /// Xử lý Login/Register bằng đúng giao thức JSON 
    /// Packet&lt;AuthRequestData&gt;/Packet&lt;AuthResponseData&gt;)
    ///

    
    public class AuthHandler
    {
        private readonly IUserRepository _userRepository;
        private readonly ClientManager _clientManager;

        public AuthHandler(IUserRepository userRepository, ClientManager clientManager)
        {
            _userRepository = userRepository;
            _clientManager = clientManager;
        }

        public async Task HandleAsync(TcpClient client)
        {
            NetworkStream stream = client.GetStream();

            try
            {
                string json = await MessageProtocol.ReceiveRawJsonAsync(stream);
                if (json == null)
                {
                    client.Close();
                    return;
                }

                // Đọc trước field "type" để biết đây là gói LOGIN hay REGISTER
                using JsonDocument doc = JsonDocument.Parse(json);
                string type = doc.RootElement.GetProperty("type").GetString();

                Packet<AuthRequestData> requestPacket = JsonSerializer.Deserialize<Packet<AuthRequestData>>(json);
                AuthRequestData request = requestPacket.Data;

                if (type == "REGISTER")
                {
                    await HandleRegisterAsync(client, stream, requestPacket.Seq, request);
                }
                else if (type == "LOGIN")
                {
                    await HandleLoginAsync(client, stream, requestPacket.Seq, request);
                }
                else
                {
                    await SendAuthResponseAsync(stream, requestPacket.Seq, 400, "Loại gói tin không hợp lệ", null);
                    client.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[AuthHandler] Lỗi không mong muốn: " + ex.Message);
                client.Close();
            }
        }

        private async Task HandleRegisterAsync(TcpClient client, NetworkStream stream, int seq, AuthRequestData request)
        {
            try
            {
                if (_userRepository.GetByUsername(request.Username) != null)
                {
                    await SendAuthResponseAsync(stream, seq, 409, "Username đã tồn tại", null);
                    client.Close();
                    return;
                }

                // Lưu ý: đang lưu password thô, chưa băm (hash).
                int userId = _userRepository.CreateUser(request.Username, request.Password, request.DisplayName);

                if (!string.IsNullOrEmpty(request.AvatarUrl))
                {
                    _userRepository.UpdateAvatar(userId, request.AvatarUrl);
                }

                UserModel newUser = new UserModel
                {
                    UserId = userId,
                    Username = request.Username,
                    DisplayName = request.DisplayName,
                    AvatarUrl = request.AvatarUrl
                };

                await SendAuthResponseAsync(stream, seq, 200, "Đăng ký thành công", newUser);

                _clientManager.Add(new ClientSession
                {
                    TcpClient = client,
                    UserId = userId,
                    Username = request.Username,
                    DisplayName = request.DisplayName
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("[AuthHandler] Lỗi khi Register: " + ex.Message);
                await SendAuthResponseAsync(stream, seq, 500, "Lỗi hệ thống, thử lại sau", null);
                client.Close();
            }
        }

        private async Task HandleLoginAsync(TcpClient client, NetworkStream stream, int seq, AuthRequestData request)
        {
            try
            {
                UserModel user = _userRepository.GetByUsername(request.Username);

                if (user == null || user.PasswordHash != request.Password)
                {
                    await SendAuthResponseAsync(stream, seq, 401, "Sai tài khoản hoặc mật khẩu", null);
                    client.Close();
                    return;
                }

                await SendAuthResponseAsync(stream, seq, 200, "Đăng nhập thành công", user);

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
                await SendAuthResponseAsync(stream, seq, 500, "Lỗi hệ thống, thử lại sau", null);
                client.Close();
            }
        }

        /// Đóng gói kết quả thành Packet&lt;AuthResponseData&gt; rồi gửi qua MessageProtocol.
        private async Task SendAuthResponseAsync(NetworkStream stream, int seq, int code, string message, UserModel user)
        {
            AuthResponseData responseData = new AuthResponseData
            {
                Code = code,
                Message = message,
                UserId = user != null ? user.UserId.ToString() : "",
                DisplayName = user != null ? user.DisplayName : "",
                AvatarUrl = user?.AvatarUrl
            };

            Packet<AuthResponseData> responsePacket = new Packet<AuthResponseData>
            {
                Type = "AUTH_RESPONSE",
                Seq = seq,
                Data = responseData
            };

            await MessageProtocol.SendPacketAsync(stream, responsePacket);
        }
    }
}