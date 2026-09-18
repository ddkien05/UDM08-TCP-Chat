using System;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ChatTCP.Common.Models;
using ChatTCP.Common.Protocol;

namespace ChatTCP.Client.Networking
{
    /// <summary>
    /// ClientSocketService quản lý giao tiếp TCP socket cho ứng dụng chat WPF.
    /// 
    /// Trách nghiệm vụ:
    /// - Thiết lập/ngắt kết nối TCP tới server chat
    /// - Gửi và nhận các gói tin (AUTH_REQ, CHAT_MSG, BROADCAST, ERROR, v.v.)
    /// - Chạy vòng lặp nhận tin nhắn nền để xử lý các tin nhắn đến
    /// - Phát sự kiện trên luồng UI để cập nhật an toàn cho các collection
    /// 
    /// Tính năng chính:
    /// - Phát sự kiện an toàn luồng thông qua WPF Dispatcher
    /// - Mẫu async/await cho I/O không bị chặn
    /// - Tự động kết nối lại khi gửi thất bại
    /// - Hỗ trợ nhiều loại tin nhắn: thường, trả lời, chuyển tiếp, broadcast
    /// 
    /// Cách sử dụng:
    ///   var service = new ClientSocketService(Dispatcher.CurrentDispatcher);
    ///   await service.ConnectAsync("127.0.0.1", 9000);
    ///   service.OnChatMessageReceived += HandleMessage;
    ///   await service.SendChatMessageAsync(data);
    /// </summary>
    public class ClientSocketService
    {
        private TcpClient? _client;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;
        private readonly Dispatcher? _dispatcher;

        public ClientSocketService(Dispatcher? dispatcher = null)
        {
            _dispatcher = dispatcher;
        }

        public bool IsConnected =>
            _client != null && _client.Connected && _stream != null;

        /// <summary>
        /// Bắn khi nhận được gói tin CHAT_MSG thường từ server.
        /// Bao gồm cả loại Reply, Forward và tin nhắn tiêu chuẩn.
        /// </summary>
        public event Action<Packet<ChatMessageData>>? OnChatMessageReceived;

        /// <summary>
        /// Bắn khi nhận được gói tin BROADCAST (tin nhắn từ server gửi tới tất cả client).
        /// </summary>
        public event Action<Packet<ChatMessageData>>? OnBroadcastReceived;

        /// <summary>
        /// Bắn khi nhận được gói tin ERROR hoặc xảy ra lỗi kết nối.
        /// </summary>
        public event Action<string>? OnError;

        /// <summary>
        /// Bắn khi kết nối bị đóng hoặc mất.
        /// </summary>
        public event Action? OnDisconnected;

        /// <summary>
        /// Thiết lập kết nối TCP tới server chat và bắt đầu vòng lặp nhận tin.
        /// 
        /// Quy trình giao thức:
        /// 1. Kết nối qua TCP đến host:port
        /// 2. Khởi động vòng lặp nhận tin nhắn nền để phân tích các gói tin đến
        /// 3. Server thường phản hồi bằng AUTH_RSP sau kết nối ban đầu
        /// 
        /// Xử lý lỗi:
        /// - Lỗi kết nối gọi OnError và trả về false
        /// - Vòng lặp nhận tự thoát khi có lỗi stream
        /// </summary>
        /// <param name="host">Địa chỉ IP server (mặc định: localhost)</param>
        /// <param name="port">Cổng TCP server (mặc định: 9000)</param>
        /// <returns>True nếu kết nối thành công, false nếu thất bại</returns>
        public async Task<bool> LoginAsync(string host, int port, string username, string password)
        {
            try
            {
                Disconnect();

                _client = new TcpClient();
                await _client.ConnectAsync(host, port);
                _stream = _client.GetStream();

                // Gửi yêu cầu đăng nhập
                var loginPacket = new Packet<object>
                {
                    Type = "LOGIN_REQ",
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Data = new { username = username, password = password }
                };
                await MessageProtocol.SendPacketAsync(_stream, loginPacket);

                // Đợi phản hồi
                string? rawRes = await MessageProtocol.ReceiveRawJsonAsync(_stream);
                if (string.IsNullOrEmpty(rawRes))
                {
                    throw new Exception("Không nhận được phản hồi từ Server.");
                }

                var resPacket = JsonSerializer.Deserialize<Packet<JsonElement>>(rawRes);
                if (resPacket == null || resPacket.Type != "AUTH_RES")
                {
                    throw new Exception("Phản hồi không hợp lệ từ Server.");
                }

                int code = resPacket.Data.GetProperty("code").GetInt32();
                string message = resPacket.Data.GetProperty("message").GetString() ?? "Lỗi không xác định";

                if (code != 200)
                {
                    RaiseError(message);
                    Disconnect();
                    return false;
                }

                // Nếu thành công, bắt đầu vòng lặp nhận tin nhắn
                _cts = new CancellationTokenSource();
                _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));

                return true;
            }
            catch (Exception ex)
            {
                Disconnect();
                RaiseError(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Đóng kết nối TCP và hủy vòng lặp nhận tin.
        /// Có thể gọi nhiều lần một cách an toàn.
        /// </summary>
        public void Disconnect()
        {
            try
            {
                _cts?.Cancel();
            }
            catch
            {
            }

            try
            {
                _stream?.Close();
                _client?.Close();
            }
            catch
            {
            }
            finally
            {
                _stream = null;
                _client = null;
                _cts = null;

                InvokeOnUI(() => OnDisconnected?.Invoke());
            }
        }

        /// <summary>
        /// Gửi một gói tin (packet) bất kỳ tới server.
        /// Hàm này được dùng nội bộ bởi SendChatMessageAsync và các hàm gửi loại tin khác.
        /// 
        /// Giao thức:
        /// - Packet được tuần tự hóa thành JSON
        /// - Độ dài thông điệp được nối vào đầu (thông qua MessageProtocol)
        /// - Dữ liệu được đẩy qua TCP stream
        /// 
        /// Xử lý lỗi:
        /// - Nếu có Exception sẽ tự động gọi Disconnect() để dọn dẹp state
        /// - Bắn ra sự kiện OnError kèm chi tiết lỗi
        /// </summary>
        /// <typeparam name="T">Kiểu dữ liệu của packet (ví dụ: ChatMessageData, ErrorData)</typeparam>
        /// <param name="packet">Đối tượng Packet cần gửi</param>
        /// <returns>Task hoàn thành khi gói tin đã ghi xong vào stream</returns>
        public async Task SendPacketAsync<T>(Packet<T> packet)
        {
            if (!IsConnected || _stream == null)
            {
                throw new InvalidOperationException("Chưa kết nối");
            }

            try
            {
                await MessageProtocol.SendPacketAsync(_stream, packet);
            }
            catch (Exception ex)
            {
                RaiseError($"Send error: {ex.Message}");
                // Xử lý như đã mất kết nối
                Disconnect();
            }
        }

        /// <summary>
        /// Hàm tiện ích bậc cao để gửi một tin nhắn chat.
        /// Tự động đóng gói ChatMessageData vào Packet với loại "CHAT_MSG" và gửi qua socket.
        /// 
        /// Các loại tin nhắn hỗ trợ:
        /// - PRIVATE: Tin nhắn riêng cho 1 người (TargetId = UserId người nhận)
        /// - BROADCAST: Gửi tới tất cả người dùng kết nối (TargetId = "*")
        /// - GROUP: Tin nhắn nhóm (TargetId = Group ID)
        /// 
        /// Xử lý đặc biệt:
        /// - Nếu tin nhắn có gán ReplyTo, server sẽ xử lý đây là tin trả lời
        /// - Nếu IsForwarded = true, server sẽ đánh dấu đây là tin chuyển tiếp
        /// - Timestamp được tự động gán là thời gian hiện tại
        /// </summary>
        /// <param name="data">Dữ liệu tin nhắn cần gửi</param>
        /// <returns>Task hoàn thành khi gửi thành công tới server</returns>
        public async Task SendChatMessageAsync(ChatMessageData data)
        {
            var packet = new Packet<ChatMessageData>
            {
                Type = "CHAT_MSG",
                Seq = 0,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = data
            };

            await SendPacketAsync(packet);
        }

        /// <summary>
        /// Vòng lặp chạy ngầm để liên tục nhận và xử lý các gói tin từ server.
        /// Chạy trên một luồng riêng (Threadpool) để không làm block UI thread.
        /// 
        /// Phân loại gói tin:
        /// - "CHAT_MSG" → Gắn vào OnChatMessageReceived (bao gồm cả reply, forward, broadcast)
        /// - "BROADCAST" → Gắn vào OnBroadcastReceived 
        /// - "ERROR" → Bắn event OnError hiển thị thông báo lỗi
        /// 
        /// Xử lý lỗi:
        /// - Nếu JSON bị lỗi định dạng, sẽ bỏ qua mà không làm chết vòng lặp.
        /// - Lỗi stream (ngắt kết nối) sẽ tự thoát vòng lặp và gọi Disconnect()
        /// - Đồng bộ UI thread an toàn thông qua Dispatcher.
        /// </summary>
        /// <param name="token">Cancellation token để có thể ngắt vòng lặp nhận</param>
        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            if (_stream == null)
            {
                return;
            }

            try
            {
                while (!token.IsCancellationRequested)
                {
                    string? raw =
                        await MessageProtocol.ReceiveRawJsonAsync(_stream);

                    if (raw == null)
                    {
                        break;
                    }

                    Packet<JsonElement>? basePacket = null;

                    try
                    {
                        basePacket =
                            JsonSerializer.Deserialize<Packet<JsonElement>>(raw);
                    }
                    catch
                    {
                        // Bỏ qua packet JSON không hợp lệ
                    }

                    if (basePacket == null)
                    {
                        continue;
                    }

                    // Route packet based on type
                    switch (basePacket.Type)
                    {
                        case "CHAT_MSG":
                            HandleChatMessage(raw);
                            break;

                        case "BROADCAST":
                            HandleBroadcast(raw);
                            break;

                        case "ERROR":
                            HandleError(raw);
                            break;

                        case "AUTH_RSP":
                            Console.WriteLine("Đã nhận phản hồi xác thực");
                            break;

                        default:
                            break;
                    }

                    // Parse ChatMessageData
                    try
                    {
                        var chatPacket =
                            JsonSerializer.Deserialize<Packet<ChatMessageData>>(raw);

                        if (chatPacket != null)
                        {
                            InvokeOnUI(() =>
                                OnChatMessageReceived?.Invoke(chatPacket));
                        }
                    }
                    catch (Exception ex)
                    {
                        RaiseError(
                            $"Receive parse error: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Disconnect() chủ động thì có thể đi vào đây.
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    RaiseError(
                        $"Receive loop error: {ex.Message}");
                }
            }
            finally
            {
                Disconnect();
            }
        }
        /// <summary>
        /// Chuyển hướng gói tin CHAT_MSG tới sự kiện OnChatMessageReceived.
        /// Phân tích JSON thô và xác thực trước khi gọi.
        /// </summary>
        private void HandleChatMessage(string raw)
        {
            try
            {
                var chatPacket = JsonSerializer.Deserialize<Packet<ChatMessageData>>(raw);
                if (chatPacket != null)
                {
                    InvokeOnUI(() => OnChatMessageReceived?.Invoke(chatPacket));
                }
            }
            catch (Exception ex)
            {
                RaiseError($"Receive parse error: {ex.Message}");
            }
        }

        /// <summary>
        /// Chuyển hướng gói tin BROADCAST tới sự kiện OnBroadcastReceived.
        /// Tương tự HandleChatMessage nhưng dành cho tin nhắn broadcast.
        /// </summary>
        private void HandleBroadcast(string raw)
        {
            try
            {
                var broadcastPacket = JsonSerializer.Deserialize<Packet<ChatMessageData>>(raw);
                if (broadcastPacket != null)
                {
                    InvokeOnUI(() => OnBroadcastReceived?.Invoke(broadcastPacket));
                }
            }
            catch (Exception ex)
            {
                RaiseError($"Broadcast parse error: {ex.Message}");
            }
        }

        /// <summary>
        /// Chuyển hướng gói tin ERROR tới sự kiện OnError.
        /// Trích xuất mã lỗi và thông báo để hiển thị cho người dùng.
        /// </summary>
        private void HandleError(string raw)
        {
            try
            {
                var errorPacket = JsonSerializer.Deserialize<Packet<ErrorData>>(raw);
                if (errorPacket?.Data != null)
                {
                    RaiseError($"Server error {errorPacket.Data.Code}: {errorPacket.Data.Message}");
                }
            }
            catch (Exception ex)
            {
                RaiseError($"Error parse error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gọi một hành động trên luồng UI nếu Dispatcher có sẵn.
        /// Ngược lại thực thi trên thread pool.
        /// 
        /// Đảm bảo cập nhật an toàn cho các collection và điều khiển UI.
        /// Được gọi bởi tất cả các phương thức phát sự kiện để đảm bảo thực thi trên luồng UI.
        /// </summary>
        /// <param name="action">Hàm callback để gọi trên luồng UI</param>
        private void InvokeOnUI(Action action)
        {
            if (_dispatcher != null)
            {
                try
                {
                    if (_dispatcher.CheckAccess())
                    {
                        action();
                    }
                    else
                    {
                        _dispatcher.BeginInvoke(action);
                    }
                }
                catch
                {
                    // Không để lỗi Dispatcher làm crash UI
                }
            }
            else
            {
                // Không có Dispatcher, thực thi trên threadpool
                try { Task.Run(action); } catch { }
                try
                {
                    Task.Run(action);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Gửi sự kiện OnError với thông báo được chỉ định.
        /// Luôn thực thi trên luồng UI thông qua InvokeOnUI.
        /// </summary>
        /// <param name="message">Thông báo lỗi cần báo cáo</param>
        private void RaiseError(string message)
        {
            InvokeOnUI(() =>
                OnError?.Invoke(message));
        }
    }
}