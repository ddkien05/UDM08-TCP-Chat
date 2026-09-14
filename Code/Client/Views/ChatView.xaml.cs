using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using ChatTCP.Client.Networking;
using ChatTCP.Common.Models;

using Client.Views;

namespace ChatTCP.Client.Views
{
    public partial class ChatView :
        UserControl
    {
        private ChatItem?
            _selectedChat;

        private LoginResponseData?
            _currentUser;

        private ClientSocketService?
            _socketService;

        public ObservableCollection<ChatMessageItem>
            Messages
        { get; } =
                new();

        private string?
            _currentReplyMsgId;

        private string?
            _currentReplySenderName;

        private string?
            _currentReplySnippet;

        // =====================================================
        // DESIGNER
        // =====================================================

        public ChatView()
        {
            InitializeComponent();

            MessagesList.ItemsSource =
                Messages;
        }

        // =====================================================
        // RUNTIME
        // =====================================================

        public ChatView(
            ChatItem selectedChat,
            LoginResponseData currentUser,
            ClientSocketService socketService)
            : this()
        {
            _selectedChat =
                selectedChat;

            _currentUser =
                currentUser;

            _socketService =
                socketService;

            DataContext =
                selectedChat;

            ChatUserNameText.Text =
                selectedChat.Name;

            UpdateOnlineStatus();

            selectedChat.PropertyChanged +=
                SelectedChat_PropertyChanged;

            socketService.OnChatMessageReceived +=
                SocketService_OnChatMessageReceived;

            socketService.OnConversationHistoryReceived +=
                SocketService_OnConversationHistoryReceived;

            socketService.OnError +=
                SocketService_OnError;

            Unloaded +=
                ChatView_Unloaded;

            if (selectedChat.ConversationId > 0)
            {
                _ =
                    socketService
                        .RequestConversationHistoryAsync(
                            selectedChat.ConversationId);
            }
        }

        // =====================================================
        // STATUS
        // =====================================================

        private void SelectedChat_PropertyChanged(
            object? sender,
            PropertyChangedEventArgs e)
        {
            if (e.PropertyName ==
                    nameof(ChatItem.IsOnline)
                ||
                e.PropertyName ==
                    nameof(ChatItem.StatusText))
            {
                UpdateOnlineStatus();
            }
        }

        private void UpdateOnlineStatus()
        {
            if (_selectedChat == null)
            {
                return;
            }

            bool online =
                _selectedChat.IsOnline;

            ChatUserStatusText.Text =
                online
                    ? "Đang hoạt động"
                    : "Ngoại tuyến";

            ChatUserStatusText.Foreground =
                new SolidColorBrush(
                    online
                        ? Color.FromRgb(
                            34,
                            197,
                            94)
                        : Color.FromRgb(
                            148,
                            163,
                            184));

            OnlineBadge.Visibility =
                online
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        // =====================================================
        // HISTORY
        // =====================================================

        private void SocketService_OnConversationHistoryReceived(
            Packet<ConversationHistoryData> packet)
        {
            if (_selectedChat == null
                ||
                _currentUser == null)
            {
                return;
            }

            if (packet.Data.ConversationId
                != _selectedChat.ConversationId)
            {
                return;
            }

            Messages.Clear();

            foreach (
                ChatMessageData message
                in packet.Data.Messages)
            {
                Messages.Add(
                    CreateMessageItem(
                        message));
            }

            ScrollToBottom();
        }

        // =====================================================
        // LIVE MESSAGE
        // =====================================================

        private void SocketService_OnChatMessageReceived(
            Packet<ChatMessageData> packet)
        {
            if (_selectedChat == null
                ||
                _currentUser == null)
            {
                return;
            }

            ChatMessageData data =
                packet.Data;

            bool belongsToCurrentChat =
                data.ConversationId > 0
                &&
                (
                    _selectedChat.ConversationId == 0
                    ||
                    data.ConversationId ==
                    _selectedChat.ConversationId
                )
                &&
                (
                    data.Sender.UserId ==
                    _selectedChat.UserId
                    ||
                    (
                        data.Sender.UserId ==
                        _currentUser.UserId
                        &&
                        data.TargetId ==
                        _selectedChat.UserId
                    )
                );

            if (!belongsToCurrentChat)
            {
                return;
            }

            // Khi chat bắt đầu từ Contacts,
            // server mới tạo conversationId.
            if (_selectedChat.ConversationId == 0)
            {
                _selectedChat.ConversationId =
                    data.ConversationId;
            }

            // tránh duplicate
            if (Messages.Any(
                    x =>
                        x.MsgId ==
                        data.MsgId))
            {
                return;
            }

            Messages.Add(
                CreateMessageItem(
                    data));

            ScrollToBottom();
        }

        // =====================================================
        // SEND
        // =====================================================

        private async void SendButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await SendCurrentMessageAsync();
        }

        private async void MessageInputBox_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            e.Handled =
                true;

            await SendCurrentMessageAsync();
        }

        private async Task SendCurrentMessageAsync()
        {
            if (_selectedChat == null
                ||
                _currentUser == null
                ||
                _socketService == null)
            {
                return;
            }

            string content =
                MessageInputBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(
                    content))
            {
                return;
            }

            ReplyInfo? reply =
                null;

            if (!string.IsNullOrWhiteSpace(
                    _currentReplyMsgId))
            {
                reply =
                    new ReplyInfo
                    {
                        MsgId =
                            _currentReplyMsgId,

                        SenderName =
                            _currentReplySenderName
                            ?? string.Empty,

                        ContentSnippet =
                            _currentReplySnippet
                            ?? string.Empty
                    };
            }

            var data =
                new ChatMessageData
                {
                    ConversationId =
                        _selectedChat
                            .ConversationId,

                    TargetType =
                        "PRIVATE",

                    TargetId =
                        _selectedChat.UserId,

                    Sender =
                        new SenderInfo
                        {
                            UserId =
                                _currentUser.UserId,

                            DisplayName =
                                _currentUser
                                    .DisplayName,

                            AvatarData =
                                _currentUser
                                    .AvatarData
                        },

                    Content =
                        content,

                    ReplyTo =
                        reply
                };

            try
            {
                // Không add local ở đây.
                // Server sẽ echo CHAT_MSG với MessageId thật.
                await _socketService
                    .SendChatMessageAsync(
                        data);

                MessageInputBox.Clear();

                ClearReplySelection();

                MessageInputBox.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message);
            }
        }

        // =====================================================
        // REPLY
        // =====================================================

        private void ReplyIcon_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button
                ||
                button.DataContext
                is not ChatMessageItem message)
            {
                return;
            }

            _currentReplyMsgId =
                message.MsgId;

            _currentReplySenderName =
                message.SenderName;

            _currentReplySnippet =
                MakeSnippet(
                    message.Content);

            ReplyComposerSenderText.Text =
                _currentReplySenderName;

            ReplyComposerContentText.Text =
                _currentReplySnippet;

            ReplyComposerBorder.Visibility =
                Visibility.Visible;

            MessageInputBox.Focus();
        }

        private void CancelReplyButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ClearReplySelection();
        }

        private void ClearReplySelection()
        {
            _currentReplyMsgId =
                null;

            _currentReplySenderName =
                null;

            _currentReplySnippet =
                null;

            ReplyComposerBorder.Visibility =
                Visibility.Collapsed;

            ReplyComposerSenderText.Text =
                string.Empty;

            ReplyComposerContentText.Text =
                string.Empty;
        }

        // =====================================================
        // CONVERT MESSAGE → UI
        // =====================================================

        private ChatMessageItem CreateMessageItem(
            ChatMessageData data)
        {
            if (_currentUser == null)
            {
                return new ChatMessageItem();
            }

            bool hasReply =
                data.ReplyTo != null;

            string replySender =
                string.Empty;

            string replyContent =
                string.Empty;

            if (data.ReplyTo != null)
            {
                bool deleted =
                    string.IsNullOrWhiteSpace(
                        data.ReplyTo.MsgId)
                    ||
                    string.IsNullOrWhiteSpace(
                        data.ReplyTo.ContentSnippet);

                if (deleted)
                {
                    replySender =
                        "Tin nhắn không khả dụng";

                    replyContent =
                        "Tin nhắn gốc đã bị xoá";
                }
                else
                {
                    replySender =
                        string.IsNullOrWhiteSpace(
                            data.ReplyTo.SenderName)
                            ? "Tin nhắn được trả lời"
                            : data.ReplyTo.SenderName;

                    replyContent =
                        data.ReplyTo.ContentSnippet;
                }
            }

            DateTimeOffset time =
                data.SentAt > 0
                    ? DateTimeOffset
                        .FromUnixTimeSeconds(
                            data.SentAt)
                        .ToLocalTime()
                    : DateTimeOffset.Now;

            return new ChatMessageItem
            {
                MsgId =
                    data.MsgId,

                SenderName =
                    data.Sender.DisplayName,

                Content =
                    data.Content,

                IsMine =
                    data.Sender.UserId ==
                    _currentUser.UserId,

                HasReply =
                    hasReply,

                ReplySenderName =
                    replySender,

                ReplyContent =
                    replyContent,

                Time =
                    time.ToString(
                        "HH:mm")
            };
        }

        // =====================================================
        // EMOJI
        // =====================================================

        private void EmojiButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageInputBox.Text +=
                "😀";

            MessageInputBox.CaretIndex =
                MessageInputBox.Text.Length;

            MessageInputBox.Focus();
        }

        // =====================================================
        // HELPERS
        // =====================================================

        private static string MakeSnippet(
            string text)
        {
            if (text.Length <= 60)
            {
                return text;
            }

            return text[..60]
                   + "...";
        }

        private void ScrollToBottom()
        {
            if (Messages.Count == 0)
            {
                return;
            }

            MessagesList.ScrollIntoView(
                Messages[^1]);
        }

        private void SocketService_OnError(
            string message)
        {
            MessageBox.Show(
                message,
                "Chat",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void ChatView_Unloaded(
            object sender,
            RoutedEventArgs e)
        {
            if (_selectedChat != null)
            {
                _selectedChat.PropertyChanged -=
                    SelectedChat_PropertyChanged;
            }

            if (_socketService != null)
            {
                _socketService.OnChatMessageReceived -=
                    SocketService_OnChatMessageReceived;

                _socketService.OnConversationHistoryReceived -=
                    SocketService_OnConversationHistoryReceived;

                _socketService.OnError -=
                    SocketService_OnError;
            }
        }
    }

    // =========================================================
    // MESSAGE UI MODEL
    // =========================================================

    public class ChatMessageItem
    {
        public string MsgId
        {
            get;
            set;
        } = string.Empty;

        public string SenderName
        {
            get;
            set;
        } = string.Empty;

        public string Content
        {
            get;
            set;
        } = string.Empty;

        public bool IsMine
        {
            get;
            set;
        }

        public bool HasReply
        {
            get;
            set;
        }

        public string ReplySenderName
        {
            get;
            set;
        } = string.Empty;

        public string ReplyContent
        {
            get;
            set;
        } = string.Empty;

        public string Time
        {
            get;
            set;
        } = string.Empty;
    }
}