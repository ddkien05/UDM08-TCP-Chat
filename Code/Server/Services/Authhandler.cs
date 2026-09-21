using System;
using System.Linq;
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
        private readonly MessageRouter _messageRouter;

        public AuthHandler(IUserRepository userRepository, ClientManager clientManager, MessageRouter messageRouter)
        {
            _userRepository = userRepository;
            _clientManager = clientManager;
            _messageRouter = messageRouter;
        }

        public async Task HandleAsync(TcpClient client)
        {
            NetworkStream stream = client.GetStream();

            try
            {
                string json = await MessageProtocol.ReceiveRawJsonAsync(stream);
                if (string.IsNullOrWhiteSpace(json))
                {
                    client.Close();
                    return;
                }

                Packet<AuthRequestData>? requestPacket = JsonSerializer.Deserialize<Packet<AuthRequestData>>(json);
                if (requestPacket == null || requestPacket.Data == null || string.IsNullOrWhiteSpace(requestPacket.Type))
                {
                    await SendAuthResponseAsync(stream, 0, 400, "Gói xác thực không hợp lệ", null);
                    client.Close();
                    return;
                }

                AuthRequestData request = requestPacket.Data;

                switch (requestPacket.Type)
                {
                    case "REGISTER":
                        await HandleRegisterAsync(client, stream, requestPacket.Seq, request);
                        break;
                    case "LOGIN":
                        await HandleLoginAsync(client, stream, requestPacket.Seq, request);
                        break;
                    default:
                        await SendAuthResponseAsync(stream, requestPacket.Seq, 400, "Loại gói tin không hợp lệ", null);
                        client.Close();
                        break;
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

                var session = new ClientSession
                {
                    TcpClient = client,
                    UserId = userId,
                    Username = request.Username,
                    DisplayName = request.DisplayName
                };
                _clientManager.Add(session);

                // Đăng ký xong coi như đã online -> tiếp tục lắng nghe CHAT_MSG từ client này
                await ListenForMessagesAsync(client, stream, session);
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

                var session = new ClientSession
                {
                    TcpClient = client,
                    UserId = user.UserId,
                    Username = user.Username,
                    DisplayName = user.DisplayName
                };
                _clientManager.Add(session);

                // QUAN TRỌNG: trước đây hàm HandleAsync kết thúc ngay tại đây,
                // nên server không bao giờ đọc tiếp CHAT_MSG của client này nữa
                // => 2 client login xong nhưng không thể chat được với nhau.
                await ListenForMessagesAsync(client, stream, session);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[AuthHandler] Lỗi khi Login: " + ex.Message);
                await SendAuthResponseAsync(stream, seq, 500, "Lỗi hệ thống, thử lại sau", null);
                client.Close();
            }
        }

        /// <summary>
        /// Vòng lặp chạy SAU KHI client đã Login/Register thành công.
        /// Liên tục đọc gói tin từ client này và định tuyến CHAT_MSG qua MessageRouter
        /// để gửi cho người nhận tương ứng. Đây là phần trước đây bị thiếu hoàn toàn:
        /// AuthHandler.HandleAsync() cũ chỉ xử lý đúng 1 gói LOGIN/REGISTER rồi return,
        /// khiến server không bao giờ đọc thêm bất kỳ CHAT_MSG nào từ client đã đăng nhập.
        /// </summary>
        private async Task ListenForMessagesAsync(TcpClient client, NetworkStream stream, ClientSession session)
        {
            try
            {
                while (client.Connected)
                {
                    string json = await MessageProtocol.ReceiveRawJsonAsync(stream);
                    if (json == null)
                    {
                        // Client đóng kết nối hoặc lỗi đọc dữ liệu
                        break;
                    }

                    Packet<JsonElement> basePacket;
                    try
                    {
                        basePacket = JsonSerializer.Deserialize<Packet<JsonElement>>(json);
                    }
                    catch
                    {
                        continue; // Bỏ qua gói JSON không hợp lệ
                    }

                    if (basePacket == null) continue;

                    switch (basePacket.Type)
                    {
                        case "CHAT_MSG":
                            var chatPacket = JsonSerializer.Deserialize<Packet<ChatMessageData>>(json);
                            if (chatPacket != null)
                            {
                                // Đảm bảo Sender luôn đúng với người thực sự đang giữ kết nối này
                                // (không tin tưởng tuyệt đối dữ liệu client tự gửi lên)
                                chatPacket.Data.Sender ??= new SenderInfo();
                                chatPacket.Data.Sender.UserId = session.UserId.ToString();
                                chatPacket.Data.Sender.DisplayName = session.DisplayName;

                                await _messageRouter.RouteChatMessageAsync(chatPacket, session);
                            }
                            break;

                        case "GET_USERS":
                            await SendUserListAsync(stream, basePacket.Seq, session);
                            break;
                        case "UPDATE_AVATAR":
                            var avatarPacket = JsonSerializer.Deserialize<Packet<UpdateAvatarData>>(json);
                            string? avatarData = avatarPacket?.Data?.AvatarUrl;
                            if (!string.IsNullOrEmpty(avatarData) && avatarData.Length < 500_000)
                            {
                                _userRepository.UpdateAvatar(session.UserId, avatarData);
                            }
                            break;

                        default:
                            Console.WriteLine($"[AuthHandler] Bỏ qua gói tin không xác định: {basePacket.Type}");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthHandler] Lỗi khi lắng nghe {session.Username}: {ex.Message}");
            }
            finally
            {
                _clientManager.Remove(client);
            }
        }

        /// <summary>
        /// Trả về danh sách toàn bộ user trong hệ thống (trừ chính người yêu cầu),
        /// kèm trạng thái online lấy từ ClientManager (đúng thời gian thực, không phụ thuộc cột IsOnline trong DB).
        /// </summary>
        private async Task SendUserListAsync(NetworkStream stream, int seq, ClientSession session)
        {
            var onlineUsernames = _clientManager.GetAll()
                .Select(s => s.Username)
                .ToHashSet();

            var users = _userRepository.GetAllUsers()
                .Where(u => u.UserId != session.UserId)
                .Select(u => new UserSummaryData
                {
                    UserId = u.UserId.ToString(),
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    AvatarUrl = u.AvatarUrl,
                    IsOnline = onlineUsernames.Contains(u.Username)
                })
                .ToList();

            var responsePacket = new Packet<UserListData>
            {
                Type = "USER_LIST",
                Seq = seq,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new UserListData { Users = users }
            };

            await session.WriteLock.WaitAsync();
            try
            {
                await MessageProtocol.SendPacketAsync(stream, responsePacket);
            }
            finally
            {
                session.WriteLock.Release();
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