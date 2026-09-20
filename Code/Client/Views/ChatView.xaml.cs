using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ChatTCP.Common.Models;
using ChatTCP.Client.Networking;

namespace ChatTCP.Client.Views
{
    public partial class ChatView : UserControl
    {
        private readonly ClientSocketService? _socketService;
        private readonly string _targetId;
        public ViewModels.ChatViewModel ViewModel { get; }

        private string? _replyMsgId;
        private string? _replySenderName;
        private string? _replySnippet;
        private bool _isForward;
        private string? _forwardFromName;
        private string? _forwardMsgId;
        private readonly List<string> _recentEmojis = new();
        private bool _skipNextEmojiToggle;

        private static readonly string[] EMOJIS = new[]
        {
            "😀","😃","😄","😁","😆","😅","😂","🙂","🙃","😉",
            "😊","😍","😘","😗","😚","😋","😛","😎","🤓","🤔",
            "👍","👎","👏","🙏","❤️","🔥","🎉","😢","😮","😴"
        };

        /// <summary>
        /// Bắn khi người dùng bấm nút quay lại (←) để trở về danh sách liên hệ.
        /// </summary>
        public event Action? BackRequested;

        /// <summary>
        /// Constructor cũ - CHỈ dùng cho design-time preview trong Visual Studio.
        /// KHÔNG dùng khi chạy thật vì socket này chưa hề đăng nhập (IsConnected = false),
        /// nên mọi tin nhắn gửi đi sẽ không tới được server.
        /// </summary>
        public ChatView() : this(new ClientSocketService(), "user_test", "Contact (demo)", false)
        {
        }

        /// <summary>
        /// Constructor thật: dùng lại đúng ClientSocketService đã LoginAsync thành công
        /// (đang chạy vòng lặp nhận tin) và targetId thật của người/nhóm sẽ chat cùng.
        /// </summary>
        public ChatView(ClientSocketService socketService, string targetId, string targetDisplayName, bool isOnline = false)
        {
            InitializeComponent();
            _socketService = socketService;
            _targetId = targetId;

            ViewModel = new ViewModels.ChatViewModel(_socketService, Dispatcher)
            {
                CurrentUser = new ChatTCP.Common.Models.SenderInfo
                {
                    UserId = _socketService.LoggedInUserId,
                    DisplayName = _socketService.LoggedInDisplayName
                },
                PeerUserId = targetId
            };
            this.DataContext = ViewModel;
            ViewModel.LoadHistoryFromStore();

            // Báo lỗi gửi tin cho người dùng (thay vì chỉ ghi Console)
            ViewModel.SendFailed += msg => MessageBox.Show(msg, "Gửi tin nhắn thất bại", MessageBoxButton.OK, MessageBoxImage.Warning);

            // ChatView bị tạo lại mỗi lần điều hướng: hủy đăng ký sự kiện socket khi rời màn hình
            // để ViewModel cũ không tiếp tục nhận tin (rò rỉ bộ nhớ + tin lọt sai khung).
            Loaded += (_, _) => ScrollToBottom();
            Unloaded += (s, e) => ViewModel.Dispose();

            ChatTargetNameText.Text = targetDisplayName;
            ChatTargetInitial.Text = string.IsNullOrWhiteSpace(targetDisplayName)
                ? "?"
                : targetDisplayName.Trim().Substring(0, 1).ToUpper();

            SetOnlineStatus(isOnline);
            EmptyChatText.Text = $"Hãy gửi lời chào đầu tiên tới {targetDisplayName}!";

            UpdateEmptyChatState();
            ViewModel.Messages.CollectionChanged += (s, e) => UpdateEmptyChatState();

            // Gắn event để auto scroll xuống dưới khi có tin nhắn mới (chỉ scroll nếu đang ở đáy)
            ViewModel.Messages.CollectionChanged += (s, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    // Nếu là tin nhắn mới thêm vào cuối, ta cuộn xuống dưới
                    if (e.NewStartingIndex == ViewModel.Messages.Count - 1)
                    {
                        ScrollToBottom();
                    }
                }
            };

            PopulateEmojis();
        }

        private async void MessageScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Kiểm tra xem user có cuộn chạm đỉnh (VerticalOffset == 0) và cuộn có ý nghĩa (Change < 0)
            if (e.VerticalChange < 0 && e.VerticalOffset == 0)
            {
                if (!ViewModel.IsLoadingHistory)
                {
                    // Ghi nhớ vị trí height hiện tại của nội dung
                    double oldHeight = MessageScrollViewer.ExtentHeight;

                    bool loaded = await ViewModel.LoadHistoryAsync();

                    if (loaded)
                    {
                        // Sau khi nạp thêm vào đầu, ExtentHeight sẽ tăng lên. 
                        // Ta cần cuộn xuống một khoảng bằng độ tăng của height để giữ nguyên vị trí mắt nhìn.
                        MessageScrollViewer.UpdateLayout();
                        double newHeight = MessageScrollViewer.ExtentHeight;
                        MessageScrollViewer.ScrollToVerticalOffset(newHeight - oldHeight);
                    }
                }
            }
        }

        // Các hàm xử lý giao diện từ SocketService cũ (OnChatMessageReceived) đã được xóa bỏ vì ViewModel tự handle.

        /// <summary>
        /// Xử lý khi người dùng bấm nút quay lại (←) ở header.
        /// </summary>
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke();
        }

        /// <summary>
        /// Cập nhật chấm + chữ trạng thái online ở header theo dữ liệu thật từ contact list.
        /// </summary>
        private void SetOnlineStatus(bool isOnline)
        {
            if (isOnline)
            {
                ChatStatusDot.Fill = (System.Windows.Media.Brush)FindResource("OnlineStatus");
                ChatStatusText.Text = "Đang hoạt động";
            }
            else
            {
                ChatStatusDot.Fill = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x9C, 0xA3, 0xAF));
                ChatStatusText.Text = "Ngoại tuyến";
            }
        }

        /// <summary>
        /// Hiện khung "chưa có tin nhắn nào" khi cuộc trò chuyện đang trống,
        /// tránh màn hình trắng trơ gây khó hiểu cho người dùng mới mở chat.
        /// </summary>
        private void UpdateEmptyChatState()
        {
            bool isEmpty = ViewModel.Messages.Count == 0;
            EmptyChatPanel.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
            MessageScrollViewer.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ScrollToBottom()
        {
            try
            {
                if (MessageScrollViewer != null)
                {
                    MessageScrollViewer.ScrollToEnd();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scrolling to bottom: {ex.Message}");
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string content = MessageInputBox.Text;
            if (string.IsNullOrWhiteSpace(content)) return;

            string targetId = _targetId; // Lấy từ contact list thực tế, truyền vào qua constructor

            if (_replyMsgId != null)
            {
                await ViewModel.SendReplyAsync(targetId, _replyMsgId, _replySenderName ?? "", _replySnippet ?? "", content);
            }
            else if (_isForward)
            {
                await ViewModel.SendForwardAsync(targetId, content, _forwardFromName ?? "");
            }
            else
            {
                await ViewModel.SendMessageAsync(targetId, content);
            }

            // Reset composer state
            MessageInputBox.Clear();
            ClearReplyState();
            ClearForwardState();
        }

        private void EmojiButton_Click(object sender, RoutedEventArgs e)
        {
            // Do StaysOpen="False", khi Popup đang mở và bấm lại nút Emoji,
            // WPF tự đóng Popup TRƯỚC (ở MouseDown) rồi Click này mới chạy (ở MouseUp).
            // Nếu không chặn lại, dòng dưới sẽ mở popup ra ngay lập tức thay vì để nó đóng.
            if (_skipNextEmojiToggle)
            {
                _skipNextEmojiToggle = false;
                return;
            }

            EmojiPopup.IsOpen = !EmojiPopup.IsOpen;
            if (EmojiPopup.IsOpen) PopulateEmojis(EmojiSearchBox.Text);
        }

        private void EmojiPopup_Closed(object? sender, EventArgs e)
        {
            _skipNextEmojiToggle = true;
        }

        private void EmojiSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PopulateEmojis(EmojiSearchBox.Text);
        }

        private void PopulateEmojis(string filter = "")
        {
            EmojiWrap.Children.Clear();
            var list = string.IsNullOrWhiteSpace(filter)
                ? EMOJIS.Concat(_recentEmojis).Distinct().ToList()
                : EMOJIS.Concat(_recentEmojis).Distinct().Where(x => x.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var emoji in list)
            {
                var btn = new Button { Content = emoji, Width = 34, Height = 34, Margin = new Thickness(2) };
                btn.Click += EmojiItem_Click;
                EmojiWrap.Children.Add(btn);
            }
        }

        private void EmojiItem_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Content is string emoji)
            {
                InsertEmojiAtCaret(emoji);
                // update recent
                _recentEmojis.Remove(emoji);
                _recentEmojis.Insert(0, emoji);
                if (_recentEmojis.Count > 20) _recentEmojis.RemoveAt(_recentEmojis.Count - 1);
                PopulateEmojis();
                // Không đóng popup ở đây nữa -> cho phép chọn nhiều icon liên tiếp.
                // Muốn đóng thì bấm lại nút Emoji (xem EmojiButton_Click).
            }
        }

        private void InsertEmojiAtCaret(string emoji)
        {
            int idx = MessageInputBox.CaretIndex;
            MessageInputBox.Text = MessageInputBox.Text.Insert(idx, emoji);
            MessageInputBox.CaretIndex = idx + emoji.Length;
            MessageInputBox.Focus();
        }

        private void ReplyMenu_Click(object sender, RoutedEventArgs e)
        {
            if (GetSelectedMessage(sender) is not ChatMessageData msg) return;

            // Dùng MsgId thật của tin gốc để server đối chiếu được tin đang được trả lời
            _replyMsgId = msg.MsgId;
            _replySenderName = GetDisplaySender(msg);
            _replySnippet = Truncate(msg.Content, 200);
            ReplyLabel.Text = $"↩ Đang trả lời {_replySenderName}";
            ReplySnippet.Text = _replySnippet;
            ReplyPreview.Visibility = Visibility.Visible;

            // Chỉ một trạng thái soạn tại một thời điểm: tắt forward nếu đang bật
            ClearForwardState();
            MessageInputBox.Focus();
        }

        private void ForwardMenu_Click(object sender, RoutedEventArgs e)
        {
            if (GetSelectedMessage(sender) is not ChatMessageData msg) return;

            _isForward = true;
            _forwardFromName = GetDisplaySender(msg);
            _forwardMsgId = msg.MsgId;
            ForwardLabel.Text = "↗ Chuyển tiếp tin nhắn";
            ForwardSnippet.Text = $"Từ {_forwardFromName} — {Truncate(msg.Content, 200)}";
            ForwardPreview.Visibility = Visibility.Visible;

            // Nội dung chuyển tiếp là nội dung tin gốc; người dùng vẫn có thể chỉnh trước khi gửi
            MessageInputBox.Text = msg.Content;
            MessageInputBox.CaretIndex = MessageInputBox.Text.Length;
            MessageInputBox.Focus();

            ClearReplyState();
        }

        private void CancelReply_Click(object sender, RoutedEventArgs e)
        {
            ClearReplyState();
        }

        private void CancelForward_Click(object sender, RoutedEventArgs e)
        {
            ClearForwardState();
        }

        private void ClearReplyState()
        {
            _replyMsgId = null;
            _replySenderName = null;
            _replySnippet = null;
            ReplyPreview.Visibility = Visibility.Collapsed;
        }

        private void ClearForwardState()
        {
            _isForward = false;
            _forwardFromName = null;
            _forwardMsgId = null;
            ForwardPreview.Visibility = Visibility.Collapsed;
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
        }

        /// <summary>
        /// Lấy ChatMessageData của bong bóng chat được bấm chuột phải (DataContext của Border).
        /// </summary>
        private static ChatMessageData? GetSelectedMessage(object sender)
        {
            if (sender is MenuItem mi && mi.Parent is ContextMenu cm &&
                cm.PlacementTarget is FrameworkElement fe)
            {
                return fe.DataContext as ChatMessageData;
            }
            return null;
        }

        /// <summary>
        /// Tên người gửi gốc dùng cho Reply/Forward. Luôn dùng DisplayName thật (kể cả tin của chính mình)
        /// vì tên này được gửi sang người nhận, nên không được dùng nhãn cục bộ như "Bạn".
        /// </summary>
        private static string GetDisplaySender(ChatMessageData msg)
        {
            return string.IsNullOrWhiteSpace(msg.Sender?.DisplayName) ? "Người dùng" : msg.Sender.DisplayName;
        }

        // Đã xóa AppendSentMessageToUi và AppendReceivedMessageToUi vì đã dùng Data Binding MVVM
    }
}