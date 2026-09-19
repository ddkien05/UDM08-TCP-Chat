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

        private static readonly string[] EMOJIS = new[]
        {
            "😀","😃","😄","😁","😆","😅","😂","🙂","🙃","😉",
            "😊","😍","😘","😗","😚","😋","😛","😎","🤓","🤔",
            "👍","👎","👏","🙏","❤️","🔥","🎉","😢","😮","😴"
        };

        /// <summary>
        /// Constructor cũ - CHỈ dùng cho design-time preview trong Visual Studio.
        /// KHÔNG dùng khi chạy thật vì socket này chưa hề đăng nhập (IsConnected = false),
        /// nên mọi tin nhắn gửi đi sẽ không tới được server.
        /// </summary>
        public ChatView() : this(new ClientSocketService(), "user_test", "Contact (demo)")
        {
        }

        /// <summary>
        /// Constructor thật: dùng lại đúng ClientSocketService đã LoginAsync thành công
        /// (đang chạy vòng lặp nhận tin) và targetId thật của người/nhóm sẽ chat cùng.
        /// </summary>
        public ChatView(ClientSocketService socketService, string targetId, string targetDisplayName)
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
                }
            };
            this.DataContext = ViewModel;

            ChatTargetNameText.Text = targetDisplayName;

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
        /// Cuộn xuống dưới cùng để hiển thị tin nhắn mới nhất
        /// </summary>
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
            EmojiPopup.IsOpen = !EmojiPopup.IsOpen;
            if (EmojiPopup.IsOpen) PopulateEmojis(EmojiSearchBox.Text);
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
                EmojiPopup.IsOpen = false;
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
            if (sender is MenuItem mi && mi.Parent is ContextMenu cm && cm.PlacementTarget is Border border)
            {
                string text = GetMessageTextFromBorder(border);
                string senderName = border.HorizontalAlignment == HorizontalAlignment.Right ? "You" : "Contact";
                _replyMsgId = Guid.NewGuid().ToString();
                _replySenderName = senderName;
                _replySnippet = Truncate(text, 200);
                ReplyLabel.Text = $"↩ Replying to {_replySenderName}";
                ReplySnippet.Text = _replySnippet;
                ReplyPreview.Visibility = Visibility.Visible;
                // hide forward if any
                ForwardPreview.Visibility = Visibility.Collapsed;
                _isForward = false;
                _forwardFromName = null;
            }
        }

        private void ForwardMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Parent is ContextMenu cm && cm.PlacementTarget is Border border)
            {
                string text = GetMessageTextFromBorder(border);
                string senderName = border.HorizontalAlignment == HorizontalAlignment.Right ? "You" : "Contact";
                _isForward = true;
                _forwardFromName = senderName;
                _forwardMsgId = Guid.NewGuid().ToString();
                ForwardLabel.Text = "↗ Forwarding message";
                ForwardSnippet.Text = $"From: {senderName} — {Truncate(text, 200)}";
                ForwardPreview.Visibility = Visibility.Visible;
                // hide reply if any
                ReplyPreview.Visibility = Visibility.Collapsed;
                _replyMsgId = null;
                _replySenderName = null;
                _replySnippet = null;
            }
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

        private string GetMessageTextFromBorder(Border border)
        {
            if (border.Child is TextBlock tb) return tb.Text;
            if (border.Child is StackPanel sp)
            {
                foreach (var child in sp.Children)
                {
                    if (child is TextBlock t) return t.Text;
                    if (child is Border b && b.Child is TextBlock t2) return t2.Text;
                }
            }
            return string.Empty;
        }

        // Đã xóa AppendSentMessageToUi và AppendReceivedMessageToUi vì đã dùng Data Binding MVVM
    }
}
