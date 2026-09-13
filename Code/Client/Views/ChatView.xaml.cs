using System;
using System.ComponentModel;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using ChatTCP.Client.Networking;
using ChatTCP.Common.Models;

using Client.Views;

namespace ChatTCP.Client.Views
{
    public partial class ChatView : UserControl
    {
        private readonly ChatItem _selectedChat;

        private readonly ClientSocketService?
            _socketService;

        // =====================================================
        // CURRENT REPLY
        // =====================================================

        private string?
            _currentReplyMsgId;

        private string?
            _currentReplySenderName;

        private string?
            _currentReplySnippet;

        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        public ChatView(
            ChatItem selectedChat,
            ClientSocketService? socketService)
        {
            InitializeComponent();

            _selectedChat =
                selectedChat;

            _socketService =
                socketService;

            DataContext =
                _selectedChat;

            ChatUserNameText.Text =
                _selectedChat.Name;

            UpdateOnlineStatus();

            _selectedChat.PropertyChanged +=
                SelectedChat_PropertyChanged;

            Unloaded +=
                ChatView_Unloaded;
        }

        // =====================================================
        // ONLINE STATUS
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
            ChatUserStatusText.Text =
                _selectedChat.IsOnline
                    ? "Đang hoạt động"
                    : "Ngoại tuyến";

            ChatUserStatusText.Foreground =
                _selectedChat.IsOnline

                    ? new SolidColorBrush(
                        Color.FromRgb(
                            34,
                            197,
                            94))

                    : new SolidColorBrush(
                        Color.FromRgb(
                            156,
                            163,
                            175));
        }

        // =====================================================
        // REPLY ICON
        // =====================================================

        private void ReplyIcon_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            string? tag =
                button.Tag as string;

            if (string.IsNullOrWhiteSpace(
                    tag))
            {
                return;
            }

            string[] parts =
                tag.Split(
                    '|',
                    3);

            if (parts.Length < 3)
            {
                return;
            }

            string msgId =
                parts[0];

            string senderName =
                parts[1];

            string content =
                parts[2];

            StartReply(
                msgId,
                senderName,
                content);
        }

        // =====================================================
        // START REPLY
        // =====================================================

        private void StartReply(
            string msgId,
            string senderName,
            string contentSnippet)
        {
            _currentReplyMsgId =
                msgId;

            _currentReplySenderName =
                string.IsNullOrWhiteSpace(
                    senderName)

                    ? "Tin nhắn được trả lời"

                    : senderName;

            _currentReplySnippet =
                GetSnippet(
                    contentSnippet);

            ReplyComposerSenderText.Text =
                _currentReplySenderName;

            ReplyComposerContentText.Text =
                _currentReplySnippet;

            ReplyComposerBorder.Visibility =
                Visibility.Visible;

            MessageInputBox.Focus();
        }

        // =====================================================
        // CANCEL REPLY
        // =====================================================

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
        // SNIPPET
        // =====================================================

        private static string GetSnippet(
            string? text)
        {
            if (string.IsNullOrWhiteSpace(
                    text))
            {
                return "Tin nhắn được trả lời";
            }

            string cleaned =
                text.Trim();

            if (cleaned.Length <= 60)
            {
                return cleaned;
            }

            return cleaned.Substring(
                       0,
                       60)
                   + "...";
        }

        // =====================================================
        // SEND
        // =====================================================

        private void SendButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            string content =
                MessageInputBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(
                    content))
            {
                return;
            }

            /*
             * Sau này khi nối TCP thật:
             *
             * ReplyInfo? replyInfo = null;
             *
             * if (_currentReplyMsgId != null)
             * {
             *     replyInfo = new ReplyInfo
             *     {
             *         MsgId = _currentReplyMsgId,
             *         SenderName =
             *             _currentReplySenderName ?? "",
             *         ContentSnippet =
             *             _currentReplySnippet ?? ""
             *     };
             * }
             *
             * var data = new ChatMessageData
             * {
             *     ...
             *     ReplyTo = replyInfo
             * };
             *
             * await _socketService.SendChatMessageAsync(data);
             */

            MessageInputBox.Clear();

            ClearReplySelection();

            MessageInputBox.Focus();
        }

        // =====================================================
        // EXISTING REPLY PREVIEW
        // =====================================================

        public void DisplayReplyPreview(
            ReplyInfo? reply,
            bool hasReplyReference)
        {
            if (!hasReplyReference)
            {
                ReplyPreviewBorder.Visibility =
                    Visibility.Collapsed;

                return;
            }

            ReplyPreviewBorder.Visibility =
                Visibility.Visible;

            // =================================================
            // ORIGINAL MESSAGE DELETED / NOT FOUND
            // =================================================

            if (reply == null)
            {
                ShowDeletedReply();

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    reply.MsgId))
            {
                ShowDeletedReply();

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    reply.ContentSnippet))
            {
                ShowDeletedReply();

                return;
            }

            // =================================================
            // ORIGINAL MESSAGE EXISTS
            // =================================================

            ReplySenderText.Text =
                string.IsNullOrWhiteSpace(
                    reply.SenderName)

                    ? "Tin nhắn được trả lời"

                    : reply.SenderName;

            ReplyContentText.Text =
                reply.ContentSnippet;

            ReplyContentText.FontStyle =
                FontStyles.Normal;

            ReplyContentText.Opacity =
                1;
        }

        // =====================================================
        // SAFE MESSAGE WRAPPER
        // =====================================================

        public void DisplayMessageReplyPreview(
            ChatMessageData? message,
            bool hasReplyReference)
        {
            DisplayReplyPreview(
                message?.ReplyTo,
                hasReplyReference);
        }

        // =====================================================
        // DELETED REPLY
        // =====================================================

        private void ShowDeletedReply()
        {
            ReplySenderText.Text =
                "Tin nhắn không khả dụng";

            ReplyContentText.Text =
                "Tin nhắn gốc đã bị xoá";

            ReplyContentText.FontStyle =
                FontStyles.Italic;

            ReplyContentText.Opacity =
                0.7;
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
        // CLEAN EVENT
        // =====================================================

        private void ChatView_Unloaded(
            object sender,
            RoutedEventArgs e)
        {
            _selectedChat.PropertyChanged -=
                SelectedChat_PropertyChanged;
        }
    }
}