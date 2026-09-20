using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using ChatTCP.Common.Models;
using ChatTCP.Common.Protocol;
using ChatTCP.Client.Networking;

namespace ChatTCP.Client.ViewModels
{
    /// <summary>
    /// ChatViewModel xử lý logic nghiệp vụ cho tin nhắn chat bao gồm:
    /// - Quản lý lịch sử tin nhắn với bộ nhớ đệm trong RAM (tối đa 500 tin nhắn)
    /// - Gửi tin nhắn thường, trả lời, chuyển tiếp và phát broadcast
    /// - Tải lịch sử tin nhắn khi người dùng cuộn lên
    /// - Quản lý trạng thái tin nhắn (bối cảnh trả lời/chuyển tiếp)
    /// 
    /// ViewModel này tách biệt giao diện khỏi logic, giúp dễ testing
    /// và tái sử dụng. Nó giao tiếp với ClientSocketService để thực hiện các thao tác mạng.
    /// 
    /// Cách sử dụng:
    ///   var vm = new ChatViewModel(socketService);
    ///   vm.Messages.Add(new ChatMessageData { ... });
    ///   await vm.SendMessageAsync("Xin chào");
    ///   await vm.SendReplyAsync(messageId, "Nội dung trả lời");
    /// </summary>
    public class ChatViewModel : IDisposable
    {
        private readonly ClientSocketService? _socketService;
        private readonly Dispatcher? _dispatcher;
        private const int MaxCachedMessages = 500;
        private const int HistoryPageSize = 20;
        private bool _isLoadingHistory = false;
        private bool _disposed;

        /// <summary>
        /// UserId của người đang chat cùng trong khung này. Nếu được gán, chỉ tin nhắn PRIVATE
        /// do đúng người này gửi tới mới hiển thị; tin của người khác không lọt vào khung chat.
        /// (Tin BROADCAST luôn hiển thị vì gửi tới tất cả.)
        /// </summary>
        public string? TargetUserId { get; set; }

        /// <summary>
        /// Bắn (trên luồng UI) khi gửi tin thất bại, để View báo cho người dùng biết
        /// thay vì chỉ ghi ra Console.
        /// </summary>
        public event Action<string>? SendFailed;

        /// <summary>
        /// Bộ sưu tập có thể quan sát các tin nhắn hiển thị trong giao diện chat.
        /// Cập nhật khi nhận tin nhắn, gửi tin nhắn hoặc tải lịch sử.
        /// </summary>
        public ObservableCollection<ChatMessageData> Messages { get; } = new();

        /// <summary>
        /// Hàng đợi lịch sử tin nhắn (các tin nhắn cũ hơn) đang chờ tải.
        /// Trong ứng dụng thực tế, đây sẽ là dữ liệu lấy từ cơ sở dữ liệu.
        /// </summary>
        private readonly Queue<ChatMessageData> _historyQueue = new();

        /// <summary>
        /// Thông tin người dùng hiện tại (đặt sau khi xác thực)
        /// </summary>
        public SenderInfo? CurrentUser { get; set; }

        /// <summary>
        /// Cho biết lịch sử đang được tải hay không (ngăn việc tải đồng thời nhiều lần)
        /// </summary>
        public bool IsLoadingHistory
        {
            get => _isLoadingHistory;
            private set => _isLoadingHistory = value;
        }

        public ChatViewModel(ClientSocketService? socketService = null, Dispatcher? dispatcher = null)
        {
            _socketService = socketService;
            _dispatcher = dispatcher;

            // Đăng ký sự kiện từ socket service
            if (_socketService != null)
            {
                _socketService.OnChatMessageReceived += HandleChatMessageReceived;
                _socketService.OnBroadcastReceived += HandleBroadcastReceived;
            }
        }

        /// <summary>
        /// Gửi một tin nhắn chat thông thường tới một người nhận cụ thể.
        /// Tin nhắn được đóng gói trong Packet với Type là "CHAT_MSG" và gửi qua socket.
        /// </summary>
        /// <param name="targetId">ID của người nhận hoặc ID của nhóm</param>
        /// <param name="content">Nội dung tin nhắn</param>
        /// <param name="targetType">Loại đối tượng nhận tin (PRIVATE, GROUP, BROADCAST)</param>
        public Task SendMessageAsync(string targetId, string content, string targetType = "PRIVATE")
            => SendCoreAsync(BuildMessage(targetId, content, targetType));

        /// <summary>
        /// Gửi một tin nhắn dạng trả lời (reply). Thuộc tính ReplyTo mang MsgId của tin gốc
        /// (để server đối chiếu), tên người gửi gốc và đoạn trích nội dung gốc.
        /// </summary>
        /// <param name="targetId">ID người nhận</param>
        /// <param name="replyToMsgId">MsgId THẬT của tin nhắn đang được trả lời</param>
        /// <param name="replySenderName">Tên người gửi của tin nhắn gốc</param>
        /// <param name="replySnippet">Đoạn trích ngắn từ tin nhắn gốc</param>
        /// <param name="content">Nội dung câu trả lời</param>
        public Task SendReplyAsync(string targetId, string replyToMsgId, string replySenderName, string replySnippet, string content)
        {
            var data = BuildMessage(targetId, content);
            data.ReplyTo = new ReplyInfo
            {
                MsgId = replyToMsgId,
                SenderName = replySenderName,
                ContentSnippet = replySnippet
            };
            return SendCoreAsync(data);
        }

        /// <summary>
        /// Gửi một tin nhắn chuyển tiếp (forward) tới người nhận mới.
        /// IsForwarded = true và ForwardFromName là tên người gửi gốc.
        /// </summary>
        public Task SendForwardAsync(string targetId, string originalContent, string forwardFromName)
        {
            var data = BuildMessage(targetId, originalContent);
            data.IsForwarded = true;
            data.ForwardFromName = forwardFromName;
            return SendCoreAsync(data);
        }

        /// <summary>
        /// Gửi một tin nhắn phát thanh (broadcast) tới toàn bộ người dùng đang kết nối.
        /// TargetType = "BROADCAST" và TargetId = "*".
        /// </summary>
        public Task SendBroadcastAsync(string content)
            => SendCoreAsync(BuildMessage("*", content, "BROADCAST"));

        /// <summary>
        /// Tạo ChatMessageData chuẩn cho mọi loại tin gửi đi (dùng chung cho Send/Reply/Forward/Broadcast).
        /// </summary>
        private ChatMessageData BuildMessage(string targetId, string content, string targetType = "PRIVATE")
        {
            return new ChatMessageData
            {
                MsgId = Guid.NewGuid().ToString("N"),
                Content = content,
                TargetType = targetType,
                TargetId = targetId,
                Sender = CurrentUser ?? new SenderInfo { DisplayName = "Me" },
                IsMine = true,
                LocalTime = DateTime.Now
            };
        }

        /// <summary>
        /// Đường gửi chung: kiểm tra đầu vào + kết nối, gửi qua socket (async, không chặn UI),
        /// chỉ thêm vào danh sách hiển thị khi gửi thành công; nếu lỗi thì bắn SendFailed.
        /// </summary>
        private async Task SendCoreAsync(ChatMessageData data)
        {
            if (string.IsNullOrWhiteSpace(data.Content)) return;

            if (_socketService == null || !_socketService.IsConnected)
            {
                RaiseSendFailed("Chưa kết nối tới server, tin nhắn chưa được gửi.");
                return;
            }

            try
            {
                await _socketService.SendChatMessageAsync(data);
                InvokeOnUI(() => Messages.Add(data));
            }
            catch (Exception ex)
            {
                RaiseSendFailed(ex.Message);
            }
        }

        private void RaiseSendFailed(string message)
        {
            InvokeOnUI(() => SendFailed?.Invoke(message));
        }

        /// <summary>
        /// Tải thêm các tin nhắn cũ khi người dùng cuộn lên trên cùng của khung chat.
        /// Hiện tại lấy dữ liệu từ hàng đợi giả lập _historyQueue. 
        /// Trong hệ thống thực tế sẽ gọi API hoặc truy vấn DB.
        /// 
        /// Cơ chế:
        /// - Lấy tối đa HistoryPageSize (20) tin nhắn mỗi lần.
        /// - Chặn gọi nhiều lần cùng lúc qua cờ IsLoadingHistory.
        /// - Chèn tin nhắn cũ vào đầu danh sách Messages.
        /// </summary>
        /// <returns>Trả về True nếu tải thành công, False nếu hết lịch sử</returns>
        public async Task<bool> LoadHistoryAsync()
        {
            if (IsLoadingHistory) return false;

            IsLoadingHistory = true;
            try
            {
                // Simulate network delay
                await Task.Delay(300);

                var batch = new List<ChatMessageData>();
                for (int i = 0; i < HistoryPageSize && _historyQueue.Count > 0; i++)
                {
                    batch.Add(_historyQueue.Dequeue());
                }

                if (batch.Count == 0)
                {
                    return false; // Hết lịch sử
                }

                InvokeOnUI(() =>
                {
                    // Thêm vào đầu bộ sưu tập (tin nhắn cũ hơn trước)
                    for (int i = batch.Count - 1; i >= 0; i--)
                    {
                        Messages.Insert(0, batch[i]);
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi tải lịch sử: {ex.Message}");
                return false;
            }
            finally
            {
                IsLoadingHistory = false;
            }
        }

        /// <summary>
        /// Điền hàng đợi lịch sử bằng các tin nhắn mẫu cũ để testing.
        /// Trong môi trường production, đây sẽ là dữ liệu lấy từ cơ sở dữ liệu qua HTTP hoặc query server.
        /// </summary>
        public void LoadFakeHistory(int count = 50)
        {
            _historyQueue.Clear();
            var now = DateTime.Now;

            for (int i = count; i > 0; i--)
            {
                _historyQueue.Enqueue(new ChatMessageData
                {
                    MsgId = Guid.NewGuid().ToString("N"),
                    Content = $"Lịch sử tin nhắn #{i}: Đây là tin nhắn cũ cách đây {i} tin nhắn",
                    TargetType = "PRIVATE",
                    TargetId = "unknown",
                    Sender = new SenderInfo { UserId = $"user_{i}", DisplayName = $"Người dùng {i}" }
                });
            }
        }

        /// <summary>
        /// Xử lý tin chat nhận từ socket service. Chỉ nhận tin PRIVATE do đúng người đang chat cùng
        /// gửi tới (khi TargetUserId đã được gán) để tin của người khác không lọt vào khung này.
        /// </summary>
        private void HandleChatMessageReceived(Packet<ChatMessageData> packet)
        {
            if (packet?.Data == null) return;

            if (!string.IsNullOrEmpty(TargetUserId) &&
                packet.Data.Sender?.UserId != TargetUserId)
            {
                return;
            }

            AddIncoming(packet);
        }

        /// <summary>
        /// Xử lý tin BROADCAST nhận từ socket service; hiển thị trong khung chat đang mở.
        /// </summary>
        private void HandleBroadcastReceived(Packet<ChatMessageData> packet)
        {
            if (packet?.Data == null) return;

            // Bỏ qua bản sao tin do chính mình phát (nếu server gửi ngược lại cho người gửi)
            if (packet.Data.Sender?.UserId == CurrentUser?.UserId) return;

            packet.Data.TargetType = "BROADCAST";
            AddIncoming(packet);
        }

        private void AddIncoming(Packet<ChatMessageData> packet)
        {
            packet.Data.IsMine = false;
            packet.Data.LocalTime = packet.Timestamp > 0
                ? DateTimeOffset.FromUnixTimeSeconds(packet.Timestamp).LocalDateTime
                : DateTime.Now;

            InvokeOnUI(() => Messages.Add(packet.Data));
        }

        /// <summary>
        /// Gọi một hành động trên luồng UI nếu Dispatcher có sẵn.
        /// Ngược lại thực thi trên thread pool.
        /// Đảm bảo cập nhật an toàn cho ObservableCollection trên luồng UI.
        /// </summary>
        private void InvokeOnUI(Action action)
        {
            if (_dispatcher != null)
            {
                try
                {
                    if (_dispatcher.CheckAccess())
                        action();
                    else
                        _dispatcher.BeginInvoke(action);
                }
                catch { }
            }
            else
            {
                try { Task.Run(action); } catch { }
            }
        }

        /// <summary>
        /// Dọn dẹp tài nguyên. Gọi khi ViewModel không còn cần thiết.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_socketService != null)
            {
                _socketService.OnChatMessageReceived -= HandleChatMessageReceived;
                _socketService.OnBroadcastReceived -= HandleBroadcastReceived;
            }
            Messages.Clear();
        }
    }
}
