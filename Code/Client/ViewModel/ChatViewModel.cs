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
    public class ChatViewModel
    {
        private readonly ClientSocketService? _socketService;
        private readonly Dispatcher? _dispatcher;
        private const int MaxCachedMessages = 500;
        private const int HistoryPageSize = 20;
        private bool _isLoadingHistory = false;

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
            }
        }

        /// <summary>
        /// Gửi một tin nhắn chat thông thường tới một người nhận cụ thể.
        /// Tin nhắn sẽ được đóng gói trong một đối tượng Packet với Type là "CHAT_MSG" và gửi qua socket.
        /// 
        /// Giao thức: Packet&lt;ChatMessageData&gt;
        ///   - Type: "CHAT_MSG"
        ///   - Data chứa: MsgId, TargetType, TargetId, Sender, Content
        /// </summary>
        /// <param name="targetId">ID của người nhận hoặc ID của nhóm</param>
        /// <param name="content">Nội dung tin nhắn</param>
        /// <param name="targetType">Loại đối tượng nhận tin (PRIVATE, GROUP, BROADCAST)</param>
        /// <returns>Task hoàn thành khi tin nhắn được gửi tới server</returns>
        public async Task SendMessageAsync(string targetId, string content, string targetType = "PRIVATE")
        {
            if (string.IsNullOrWhiteSpace(content)) return;
            if (_socketService == null || !_socketService.IsConnected)
            {
                InvokeOnUI(() => 
                {
                    var msg = new ChatMessageData
                    {
                        MsgId = Guid.NewGuid().ToString("N"),
                        Content = content,
                        TargetType = targetType,
                        TargetId = targetId,
                        Sender = CurrentUser ?? new SenderInfo { DisplayName = "Me" }
                    };
                    Messages.Add(msg);
                });
                return;
            }

            try
            {
                var data = new ChatMessageData
                {
                    MsgId = Guid.NewGuid().ToString("N"),
                    Content = content,
                    TargetType = targetType,
                    TargetId = targetId,
                    Sender = CurrentUser ?? new SenderInfo { DisplayName = "Me" }
                };

                await _socketService.SendChatMessageAsync(data);

                // Optionally add to local cache immediately for optimistic UI
                InvokeOnUI(() => Messages.Add(data));
            }
            catch (Exception ex)
            {
                InvokeOnUI(() => 
                {
                    // Could raise an error event here
                    Console.WriteLine($"Send error: {ex.Message}");
                });
            }
        }

        /// <summary>
        /// Gửi một tin nhắn dạng trả lời (reply) tới một tin nhắn cụ thể trước đó.
        /// Tin nhắn reply sẽ chứa thông tin tham chiếu tới tin nhắn gốc thông qua thuộc tính ReplyInfo.
        /// 
        /// Giao thức: Tương tự SendMessageAsync, nhưng thuộc tính Data.ReplyTo sẽ được điền thông tin
        ///   - ReplyInfo chứa: MsgId (của tin gốc), SenderName (tên người gửi gốc), ContentSnippet (trích dẫn nội dung gốc)
        /// </summary>
        /// <param name="targetId">ID người nhận</param>
        /// <param name="replyToMsgId">ID của tin nhắn đang được trả lời</param>
        /// <param name="replySenderName">Tên người gửi của tin nhắn gốc</param>
        /// <param name="replySnippet">Đoạn trích dẫn ngắn từ tin nhắn gốc</param>
        /// <param name="content">Nội dung câu trả lời</param>
        public async Task SendReplyAsync(string targetId, string replyToMsgId, string replySenderName, string replySnippet, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return;
            if (_socketService == null || !_socketService.IsConnected) return;

            try
            {
                var data = new ChatMessageData
                {
                    MsgId = Guid.NewGuid().ToString("N"),
                    Content = content,
                    TargetType = "PRIVATE",
                    TargetId = targetId,
                    Sender = CurrentUser ?? new SenderInfo { DisplayName = "Me" },
                    ReplyTo = new ReplyInfo
                    {
                        MsgId = replyToMsgId,
                        SenderName = replySenderName,
                        ContentSnippet = replySnippet
                    }
                };

                await _socketService.SendChatMessageAsync(data);
                InvokeOnUI(() => Messages.Add(data));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send reply error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gửi một tin nhắn được chuyển tiếp (forward) tới một người nhận mới.
        /// Cờ IsForwarded sẽ được bật để hiển thị đây là tin nhắn chuyển tiếp.
        /// 
        /// Giao thức: Tương tự SendMessageAsync, nhưng có IsForwarded = true và ForwardFromName được gán
        /// </summary>
        /// <param name="targetId">ID người nhận mới</param>
        /// <param name="originalContent">Nội dung của tin nhắn đang được chuyển tiếp</param>
        /// <param name="forwardFromName">Tên của người gửi gốc của tin nhắn</param>
        public async Task SendForwardAsync(string targetId, string originalContent, string forwardFromName)
        {
            if (string.IsNullOrWhiteSpace(originalContent)) return;
            if (_socketService == null || !_socketService.IsConnected) return;

            try
            {
                var data = new ChatMessageData
                {
                    MsgId = Guid.NewGuid().ToString("N"),
                    Content = originalContent,
                    TargetType = "PRIVATE",
                    TargetId = targetId,
                    Sender = CurrentUser ?? new SenderInfo { DisplayName = "Me" },
                    IsForwarded = true,
                    ForwardFromName = forwardFromName
                };

                await _socketService.SendChatMessageAsync(data);
                InvokeOnUI(() => Messages.Add(data));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send forward error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gửi một tin nhắn phát thanh (broadcast) tới toàn bộ người dùng đang kết nối.
        /// Đặt TargetType thành BROADCAST để server tự phân phối tin nhắn này.
        /// 
        /// Giao thức: Giống cấu trúc CHAT_MSG, nhưng TargetType = "BROADCAST" và TargetId = "*"
        /// </summary>
        /// <param name="content">Nội dung tin nhắn phát thanh</param>
        public async Task SendBroadcastAsync(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return;
            if (_socketService == null || !_socketService.IsConnected) return;

            try
            {
                var data = new ChatMessageData
                {
                    MsgId = Guid.NewGuid().ToString("N"),
                    Content = content,
                    TargetType = "BROADCAST",
                    TargetId = "*", // Special marker for broadcast
                    Sender = CurrentUser ?? new SenderInfo { DisplayName = "Me" }
                };

                await _socketService.SendChatMessageAsync(data);
                InvokeOnUI(() => Messages.Add(data));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send broadcast error: {ex.Message}");
            }
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
        /// Xử lý tin nhắn chat tiếp nhận từ socket service.
        /// Thêm các tin nhắn nhận được vào bộ sưu tập Messages trên luồng UI.
        /// </summary>
        private void HandleChatMessageReceived(Packet<ChatMessageData> packet)
        {
            if (packet?.Data == null) return;

            InvokeOnUI(() =>
            {
                Messages.Add(packet.Data);
            });
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
            if (_socketService != null)
            {
                _socketService.OnChatMessageReceived -= HandleChatMessageReceived;
            }
            Messages.Clear();
        }
    }
}
