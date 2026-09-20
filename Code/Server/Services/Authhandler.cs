
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
   
    /// Xử lý Login/Register và tiếp tục nhận các packet
    /// từ client sau khi xác thực thành công.
 
    public class AuthHandler
    {
        private readonly IUserRepository _userRepository;
        private readonly ClientManager _clientManager;
        private readonly MessageRouter _messageRouter;

        public AuthHandler(
            IUserRepository userRepository,
            ClientManager clientManager,
            MessageRouter messageRouter)
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

                // 1. CHỜ LOGIN / REGISTER


                string json = await MessageProtocol.ReceiveRawJsonAsync(stream);

                Console.WriteLine("[AuthHandler] Đã nhận: " + json);

                if (string.IsNullOrWhiteSpace(json))
                {
                    client.Close();
                    return;
                }

                using JsonDocument doc = JsonDocument.Parse(json);

                string type =
                    doc.RootElement
                       .GetProperty("type")
                       .GetString()
                    ?? "";

                Packet<AuthRequestData>? requestPacket =
                    JsonSerializer.Deserialize<Packet<AuthRequestData>>(json);

                if (requestPacket == null || requestPacket.Data == null)
                {
                    await SendAuthResponseAsync(
                        stream,
                        0,
                        400,
                        "Packet Login/Register không hợp lệ.",
                        null);

                    client.Close();
                    return;
                }

                AuthRequestData request = requestPacket.Data;

                // 2. XỬ LÝ LOGIN / REGISTER


                if (type == "REGISTER")
                {
                    await HandleRegisterAsync(
                        client,
                        stream,
                        requestPacket.Seq,
                        request);
                }
                else if (type == "LOGIN")
                {
                    await HandleLoginAsync(
                        client,
                        stream,
                        requestPacket.Seq,
                        request);
                }
                else
                {
                    await SendAuthResponseAsync(
                        stream,
                        requestPacket.Seq,
                        400,
                        "Loại gói tin không hợp lệ.",
                        null);

                    client.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "[AuthHandler] Lỗi không mong muốn: " +
                    ex.Message);

                try
                {
                    client.Close();
                }
                catch
                {
                }
            }
        }

        // REGISTER


        private async Task HandleRegisterAsync(
            TcpClient client,
            NetworkStream stream,
            int seq,
            AuthRequestData request)
        {
            try
            {
                if (_userRepository.GetByUsername(request.Username) != null)
                {
                    await SendAuthResponseAsync(
                        stream,
                        seq,
                        409,
                        "Username đã tồn tại.",
                        null);

                    client.Close();
                    return;
                }

                // Hiện tại project vẫn đang lưu password dạng plain text.
                int userId =
                    _userRepository.CreateUser(
                        request.Username,
                        request.Password,
                        request.DisplayName);

                if (!string.IsNullOrEmpty(request.AvatarUrl))
                {
                    _userRepository.UpdateAvatar(
                        userId,
                        request.AvatarUrl);
                }

                UserModel newUser = new UserModel
                {
                    UserId = userId,
                    Username = request.Username,
                    DisplayName = request.DisplayName,
                    AvatarUrl = request.AvatarUrl
                };

                await SendAuthResponseAsync(
                    stream,
                    seq,
                    200,
                    "Đăng ký thành công.",
                    newUser);

                // Đăng ký xong -> đưa client vào danh sách online.
                ClientSession session = new ClientSession
                {
                    TcpClient = client,
                    UserId = userId,
                    Username = request.Username,
                    DisplayName = request.DisplayName
                };

                _clientManager.Add(session);

                Console.WriteLine(
                    $"[AuthHandler] {request.Username} đã Register thành công.");

                // Tiếp tục chờ CHAT_MSG.
                await ReceiveClientMessagesAsync(
                    client,
                    session);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "[AuthHandler] Lỗi khi Register: " +
                    ex.Message);

                try
                {
                    await SendAuthResponseAsync(
                        stream,
                        seq,
                        500,
                        "Lỗi hệ thống, thử lại sau.",
                        null);
                }
                catch
                {
                }

                client.Close();
            }
        }

        // LOGIN
     

        private async Task HandleLoginAsync(
            TcpClient client,
            NetworkStream stream,
            int seq,
            AuthRequestData request)
        {
            try
            {
                UserModel? user =
                    _userRepository.GetByUsername(
                        request.Username);

                if (user == null ||
                    user.PasswordHash != request.Password)
                {
                    await SendAuthResponseAsync(
                        stream,
                        seq,
                        401,
                        "Sai tài khoản hoặc mật khẩu.",
                        null);

                    client.Close();
                    return;
                }

                await SendAuthResponseAsync(
                    stream,
                    seq,
                    200,
                    "Đăng nhập thành công.",
                    user);

                ClientSession session = new ClientSession
                {
                    TcpClient = client,
                    UserId = user.UserId,
                    Username = user.Username,
                    DisplayName = user.DisplayName
                };

                _clientManager.Add(session);

                Console.WriteLine(
                    $"[AuthHandler] {user.Username} đã Login thành công.");

                // QUAN TRỌNG:
                // Sau Login không đóng HandleAsync.
                // Server tiếp tục đọc CHAT_MSG từ client này.
      

                await ReceiveClientMessagesAsync(
                    client,
                    session);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "[AuthHandler] Lỗi khi Login: " +
                    ex.Message);

                try
                {
                    await SendAuthResponseAsync(
                        stream,
                        seq,
                        500,
                        "Lỗi hệ thống, thử lại sau.",
                        null);
                }
                catch
                {
                }

                client.Close();
            }
        }

     
        // NHẬN PACKET SAU KHI LOGIN
    

        private async Task ReceiveClientMessagesAsync(
            TcpClient client,
            ClientSession session)
        {
            NetworkStream stream = client.GetStream();

            Console.WriteLine(
                $"[AuthHandler] Bắt đầu nhận dữ liệu từ {session.Username}...");

            try
            {
                while (client.Connected)
                {
                    string json =
                        await MessageProtocol.ReceiveRawJsonAsync(
                            stream);

                    if (string.IsNullOrWhiteSpace(json))
                    {
                        Console.WriteLine(
                            $"[AuthHandler] {session.Username} đã ngắt kết nối.");

                        break;
                    }

                    Console.WriteLine(
                        $"[AuthHandler] Nhận từ {session.Username}: {json}");

                    try
                    {
                        using JsonDocument document =
                            JsonDocument.Parse(json);

                        string type =
                            document.RootElement
                                .GetProperty("type")
                                .GetString()
                            ?? "";

                    
                        // CHAT_MSG
                    

                        if (type == "CHAT_MSG")
                        {
                            Packet<ChatMessageData>? chatPacket =
                                JsonSerializer.Deserialize<
                                    Packet<ChatMessageData>>(json);

                            if (chatPacket == null ||
                                chatPacket.Data == null)
                            {
                                Console.WriteLine(
                                    "[AuthHandler] CHAT_MSG không hợp lệ.");

                                continue;
                            }

                            // Bảo đảm UserId của Sender là user
                            // đã đăng nhập thật sự.
                            if (chatPacket.Data.Sender == null)
                            {
                                chatPacket.Data.Sender =
                                    new SenderInfo();
                            }

                            chatPacket.Data.Sender.UserId =
                                session.UserId.ToString();

                            if (string.IsNullOrWhiteSpace(
                                    chatPacket.Data.Sender.DisplayName))
                            {
                                chatPacket.Data.Sender.DisplayName =
                                    session.DisplayName;
                            }

                            Console.WriteLine(
                                $"[AuthHandler] " +
                                $"CHAT_MSG: {session.Username} " +
                                $"-> {chatPacket.Data.TargetId}");

                            await _messageRouter.RouteChatMessageAsync(
                                chatPacket,
                                stream);
                        }
                        else
                        {
                            Console.WriteLine(
                                $"[AuthHandler] " +
                                $"Chưa hỗ trợ packet type: {type}");
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine(
                            "[AuthHandler] JSON không hợp lệ: " +
                            ex.Message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            "[AuthHandler] Lỗi xử lý packet: " +
                            ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[AuthHandler] " +
                    $"Kết nối {session.Username} bị đóng: " +
                    ex.Message);
            }
            finally
            {
                // Client disconnect -> xóa khỏi danh sách online.
                _clientManager.Remove(client);
            }
        }

        // GỬI AUTH RESPONSE


        private async Task SendAuthResponseAsync(
            NetworkStream stream,
            int seq,
            int code,
            string message,
            UserModel? user)
        {
            AuthResponseData responseData =
                new AuthResponseData
                {
                    Code = code,
                    Message = message,
                    UserId =
                        user != null
                            ? user.UserId.ToString()
                            : "",
                    DisplayName =
                        user != null
                            ? user.DisplayName
                            : "",
                    AvatarUrl =
                        user?.AvatarUrl
                };

            Packet<AuthResponseData> responsePacket =
                new Packet<AuthResponseData>
                {
                    Type = "AUTH_RESPONSE",
                    Seq = seq,
                    Timestamp =
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Data = responseData
                };

            await MessageProtocol.SendPacketAsync(
                stream,
                responsePacket);
        }
    }
}
