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

        public ChatView()
        {
            InitializeComponent();
            _socketService = new ClientSocketService(Dispatcher);
            PopulateEmojis();
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string content = MessageInputBox.Text;
            if (string.IsNullOrWhiteSpace(content)) return;

            var data = new ChatMessageData
            {
                MsgId = Guid.NewGuid().ToString(),
                TargetType = "PRIVATE",
                TargetId = string.Empty, // Integrate with conversation id if available
                Sender = new SenderInfo { UserId = string.Empty, DisplayName = "Me" },
                Content = content,
                ReplyTo = _replyMsgId != null ? new ReplyInfo { MsgId = _replyMsgId, SenderName = _replySenderName ?? string.Empty, ContentSnippet = _replySnippet ?? string.Empty } : null,
                IsForwarded = _isForward,
                ForwardFromName = _forwardFromName
            };

            try
            {
                if (_socketService != null && _socketService.IsConnected)
                {
                    await _socketService.SendChatMessageAsync(data);
                }
                else
                {
                    // Not connected: append message to UI for demo/optimistic feedback
                    AppendSentMessageToUi(data);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Send error: " + ex.Message);
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

        private void AppendSentMessageToUi(ChatMessageData data)
        {
            var border = new Border
            {
                Background = (System.Windows.Media.Brush)FindResource("PrimaryBrush"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12,8,12,8),
                Margin = new Thickness(0,4,0,4),
                HorizontalAlignment = HorizontalAlignment.Right,
                MaxWidth = 300
            };

            var tb = new TextBlock
            {
                Text = data.Content,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = (System.Windows.Media.FontFamily)FindResource("AppFontFamily"),
                FontSize = 14,
                Foreground = System.Windows.Media.Brushes.White
            };

            border.Child = tb;
            // add context menu to new message
            var cm = new ContextMenu();
            var mi1 = new MenuItem { Header = "Reply" };
            mi1.Click += ReplyMenu_Click;
            var mi2 = new MenuItem { Header = "Forward" };
            mi2.Click += ForwardMenu_Click;
            cm.Items.Add(mi1);
            cm.Items.Add(mi2);
            border.ContextMenu = cm;

            MessageStack.Children.Add(border);
        }
    }
}
