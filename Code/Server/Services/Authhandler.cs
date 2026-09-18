using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ChatTCP.Common.Models;
using ChatTCP.Common.Protocol;
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

        public async Task HandleAsync(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();

                string rawJson = await MessageProtocol.ReceiveRawJsonAsync(stream);
                if (string.IsNullOrEmpty(rawJson))
                {
                    client.Close();
                    return;
                }

                var packet = JsonSerializer.Deserialize<Packet<JsonElement>>(rawJson);
                if (packet == null)
                {
                    await SendAuthResAsync(stream, 400, "Invalid packet");
                    client.Close();
                    return;
                }

                if (packet.Type == "REGISTER_REQ")
                {
                    string username = packet.Data.GetProperty("username").GetString() ?? "";
                    string password = packet.Data.GetProperty("password").GetString() ?? "";
                    string displayName = packet.Data.TryGetProperty("display_name", out var dn) ? dn.GetString() : username;
                    
                    await HandleRegisterAsync(client, stream, username, password, displayName);
                }
                else if (packet.Type == "LOGIN_REQ" || packet.Type == "AUTH_REQ")
                {
                    string username = packet.Data.GetProperty("username").GetString() ?? "";
                    string password = packet.Data.GetProperty("password").GetString() ?? "";
                    
                    await HandleLoginAsync(client, stream, username, password);
                }
                else
                {
                    await SendAuthResAsync(stream, 400, "Lệnh không hợp lệ");
                    client.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[AuthHandler] Lỗi không mong muốn: " + ex.Message);
                client.Close();
            }
        }

        private async Task HandleRegisterAsync(TcpClient client, NetworkStream stream, string username, string password, string displayName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    await SendAuthResAsync(stream, 400, "Username và password không được để trống");
                    client.Close();
                    return;
                }

                if (_userRepository.GetByUsername(username) != null)
                {
                    await SendAuthResAsync(stream, 400, "Username đã tồn tại");
                    client.Close();
                    return;
                }

                int userId = _userRepository.CreateUser(username, password, displayName);
                
                await SendAuthResAsync(stream, 200, "Success", userId);

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
                await SendAuthResAsync(stream, 500, "Lỗi hệ thống, thử lại sau");
                client.Close();
            }
        }

        private async Task HandleLoginAsync(TcpClient client, NetworkStream stream, string username, string password)
        {
            try
            {
                UserModel user = _userRepository.GetByUsername(username);

                if (user == null || user.PasswordHash != password)
                {
                    await SendAuthResAsync(stream, 401, "Sai tài khoản hoặc mật khẩu");
                    client.Close();
                    return;
                }

                await SendAuthResAsync(stream, 200, "Success", user.UserId);

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
                await SendAuthResAsync(stream, 500, "Lỗi hệ thống, thử lại sau");
                client.Close();
            }
        }

        private async Task SendAuthResAsync(NetworkStream stream, int code, string message, int? userId = null)
        {
            var res = new Packet<object>
            {
                Type = "AUTH_RES",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new
                {
                    code = code,
                    message = message,
                    user_id = userId
                }
            };
            await MessageProtocol.SendPacketAsync(stream, res);
        }
    }
}