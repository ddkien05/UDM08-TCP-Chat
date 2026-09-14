using Microsoft.Win32;

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using ChatTCP.Client.Networking;
using ChatTCP.Client.Views;
using ChatTCP.Common.Models;

namespace Client.Views
{
    public partial class ContactListView :
        UserControl
    {
        private enum ListMode
        {
            Chats,
            OnlineContacts
        }

        private ListMode _currentMode =
            ListMode.Chats;

        public ObservableCollection<ChatItem>
            Chats
        { get; } =
                new();

        private readonly ICollectionView
            _chatView;

        private readonly ClientSocketService
            _socketService;

        private readonly LoginResponseData
            _currentUser;

        public event Action?
            LogoutRequested;

        public ContactListView(
            ClientSocketService socketService,
            LoginResponseData currentUser)
        {
            InitializeComponent();

            _socketService =
                socketService;

            _currentUser =
                currentUser;

            _chatView =
                CollectionViewSource
                    .GetDefaultView(
                        Chats);

            _chatView.Filter =
                FilterChat;

            ChatList.ItemsSource =
                _chatView;

            // SOCKET EVENTS
            _socketService.OnOnlineUsersReceived +=
                SocketService_OnOnlineUsersReceived;

            _socketService.OnConversationListReceived +=
                SocketService_OnConversationListReceived;

            _socketService.OnUserStatusChanged +=
                SocketService_OnUserStatusChanged;

            _socketService.OnUserProfileChanged +=
                SocketService_OnUserProfileChanged;

            _socketService.OnAvatarUpdated +=
                SocketService_OnAvatarUpdated;

            _socketService.OnChatMessageReceived +=
                SocketService_OnChatMessageReceived;

            _socketService.OnError +=
                SocketService_OnError;

            Loaded +=
                ContactListView_Loaded;

            Unloaded +=
                ContactListView_Unloaded;

            SetMyAvatar(
                currentUser.AvatarData);

            UpdateMenuVisuals();

            UpdateEmptyState();
        }

        // =====================================================
        // LOAD DEFAULT CHAT LIST
        // =====================================================

        private async void ContactListView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            await LoadChatsAsync();
        }

        // =====================================================
        // CHAT MENU
        // =====================================================

        private async void ChatButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await LoadChatsAsync();
        }

        private async Task LoadChatsAsync()
        {
            _currentMode =
                ListMode.Chats;

            ListTitleText.Text =
                "Danh sách chat";

            ListSubtitleText.Text =
                "Các cuộc trò chuyện gần đây";

            UpdateMenuVisuals();

            await _socketService
                .RequestConversationListAsync();
        }

        // =====================================================
        // CONTACTS MENU
        // =====================================================

        private async void ContactsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _currentMode =
                ListMode.OnlineContacts;

            ListTitleText.Text =
                "Người đang online";

            ListSubtitleText.Text =
                "Chọn một người để bắt đầu trò chuyện";

            UpdateMenuVisuals();

            await _socketService
                .RequestOnlineUsersAsync();
        }

        private void UpdateMenuVisuals()
        {
            ChatMenuBackground.Background =
                new SolidColorBrush(
                    _currentMode ==
                    ListMode.Chats
                        ? Colors.White
                        : Color.FromArgb(
                            48,
                            255,
                            255,
                            255));

            ContactsMenuBackground.Background =
                new SolidColorBrush(
                    _currentMode ==
                    ListMode.OnlineContacts
                        ? Colors.White
                        : Color.FromArgb(
                            48,
                            255,
                            255,
                            255));
        }

        // =====================================================
        // CHAT LIST RESPONSE
        // =====================================================

        private void SocketService_OnConversationListReceived(
            Packet<ConversationListData> packet)
        {
            if (_currentMode !=
                ListMode.Chats)
            {
                return;
            }

            Chats.Clear();

            foreach (
                ConversationListItemData item
                in packet.Data.Conversations)
            {
                Chats.Add(
                    new ChatItem
                    {
                        ConversationId =
                            item.ConversationId,

                        UserId =
                            item.OtherUserId,

                        Name =
                            item.DisplayName,

                        Username =
                            item.Username,

                        AvatarData =
                            item.AvatarData,

                        AvatarImage =
                            Base64ToBitmap(
                                item.AvatarData),

                        IsOnline =
                            item.IsOnline,

                        LastMessage =
                            item.LastMessage,

                        Time =
                            item.LastMessageTime
                    });
            }

            _chatView.Refresh();

            UpdateEmptyState();
        }

        // =====================================================
        // ONLINE USERS
        // =====================================================

        private void SocketService_OnOnlineUsersReceived(
            Packet<OnlineUsersData> packet)
        {
            if (_currentMode !=
                ListMode.OnlineContacts)
            {
                return;
            }

            Chats.Clear();

            foreach (
                OnlineUserData user
                in packet.Data.Users)
            {
                Chats.Add(
                    new ChatItem
                    {
                        ConversationId =
                            0,

                        UserId =
                            user.UserId,

                        Name =
                            user.DisplayName,

                        Username =
                            user.Username,

                        AvatarData =
                            user.AvatarData,

                        AvatarImage =
                            Base64ToBitmap(
                                user.AvatarData),

                        IsOnline =
                            true,

                        LastMessage =
                            "Đang online",

                        Time =
                            string.Empty
                    });
            }

            _chatView.Refresh();

            UpdateEmptyState();
        }

        // =====================================================
        // ONLINE / OFFLINE REALTIME
        // =====================================================

        private async void SocketService_OnUserStatusChanged(
            Packet<UserStatusNotifyData> packet)
        {
            if (_currentMode ==
                ListMode.OnlineContacts)
            {
                // Contact list chỉ chứa online users,
                // nên refresh lại từ server.
                await _socketService
                    .RequestOnlineUsersAsync();

                return;
            }

            ChatItem? existing =
                Chats.FirstOrDefault(
                    x =>
                        x.UserId ==
                        packet.Data.UserId);

            if (existing != null)
            {
                existing.IsOnline =
                    string.Equals(
                        packet.Data.Status,
                        "ONLINE",
                        StringComparison
                            .OrdinalIgnoreCase);
            }
        }

        // =====================================================
        // OTHER USER AVATAR CHANGED
        // =====================================================

        private void SocketService_OnUserProfileChanged(
            Packet<UserProfileNotifyData> packet)
        {
            ChatItem? user =
                Chats.FirstOrDefault(
                    x =>
                        x.UserId ==
                        packet.Data.UserId);

            if (user != null)
            {
                user.AvatarData =
                    packet.Data.AvatarData;

                user.AvatarImage =
                    Base64ToBitmap(
                        packet.Data.AvatarData);
            }
        }

        // =====================================================
        // MY AVATAR UPDATED
        // =====================================================

        private void SocketService_OnAvatarUpdated(
            Packet<AvatarUpdateResponseData> packet)
        {
            if (!packet.Data.Success)
            {
                return;
            }

            _currentUser.AvatarData =
                packet.Data.AvatarData;

            SetMyAvatar(
                packet.Data.AvatarData);
        }

        // =====================================================
        // NEW MESSAGE → REFRESH CHAT LIST
        // =====================================================

        private async void SocketService_OnChatMessageReceived(
            Packet<ChatMessageData> packet)
        {
            if (_currentMode ==
                ListMode.Chats)
            {
                await _socketService
                    .RequestConversationListAsync();
            }
        }

        // =====================================================
        // OPEN CHAT
        // =====================================================

        private void ChatList_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (ChatList.SelectedItem
                is not ChatItem selected)
            {
                return;
            }

            ChatContentHost.Content =
                new ChatView(
                    selected,
                    _currentUser,
                    _socketService);

            NoChatSelectedPanel.Visibility =
                Visibility.Collapsed;
        }

        // =====================================================
        // SEARCH
        // =====================================================

        private bool FilterChat(
            object item)
        {
            if (item is not ChatItem chat)
            {
                return false;
            }

            string keyword =
                SearchTextBox?
                    .Text?
                    .Trim()
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(
                    keyword))
            {
                return true;
            }

            return chat.Name.Contains(
                       keyword,
                       StringComparison
                           .CurrentCultureIgnoreCase)
                   ||
                   chat.Username.Contains(
                       keyword,
                       StringComparison
                           .CurrentCultureIgnoreCase);
        }

        private void SearchTextBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            _chatView.Refresh();

            UpdateEmptyState();
        }

        // =====================================================
        // EMPTY
        // =====================================================

        private void UpdateEmptyState()
        {
            bool hasVisible =
                _chatView
                    .Cast<object>()
                    .Any();

            ChatList.Visibility =
                hasVisible
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            EmptyPanel.Visibility =
                hasVisible
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            if (hasVisible)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(
                    SearchTextBox?.Text))
            {
                EmptyMessage.Text =
                    "Không tìm thấy kết quả";

                return;
            }

            EmptyMessage.Text =
                _currentMode ==
                ListMode.Chats
                    ? "Chưa có cuộc trò chuyện"
                    : "Không có người dùng nào đang online";
        }

        // =====================================================
        // CHANGE MY AVATAR
        // =====================================================

        private async void ChangeAvatarButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var dialog =
                new OpenFileDialog
                {
                    Title =
                        "Chọn ảnh đại diện",

                    Filter =
                        "Ảnh (*.png;*.jpg;*.jpeg)|" +
                        "*.png;*.jpg;*.jpeg",

                    Multiselect =
                        false
                };

            if (dialog.ShowDialog()
                != true)
            {
                return;
            }

            try
            {
                byte[] bytes =
                    File.ReadAllBytes(
                        dialog.FileName);

                // 1MB để demo TCP ổn định.
                if (bytes.Length >
                    1024 * 1024)
                {
                    MessageBox.Show(
                        "Avatar tối đa 1 MB.",
                        "Avatar",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                string base64 =
                    Convert.ToBase64String(
                        bytes);

                await _socketService
                    .UpdateAvatarAsync(
                        base64);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Avatar",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SetMyAvatar(
            string? avatarData)
        {
            BitmapImage? image =
                Base64ToBitmap(
                    avatarData);

            MyAvatarImage.Source =
                image;

            DefaultAvatarIcon.Visibility =
                image == null
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        public static BitmapImage? Base64ToBitmap(
            string? base64)
        {
            if (string.IsNullOrWhiteSpace(
                    base64))
            {
                return null;
            }

            try
            {
                byte[] bytes =
                    Convert.FromBase64String(
                        base64);

                using var stream =
                    new MemoryStream(
                        bytes);

                var bitmap =
                    new BitmapImage();

                bitmap.BeginInit();

                bitmap.CacheOption =
                    BitmapCacheOption.OnLoad;

                bitmap.StreamSource =
                    stream;

                bitmap.EndInit();

                bitmap.Freeze();

                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        // =====================================================
        // SETTINGS
        // =====================================================

        private void SettingsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (SettingsButton.ContextMenu
                == null)
            {
                return;
            }

            SettingsButton.ContextMenu
                .PlacementTarget =
                    SettingsButton;

            SettingsButton.ContextMenu
                .IsOpen =
                    true;
        }

        private void LogoutMenuItem_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageBoxResult result =
                MessageBox.Show(
                    "Bạn có chắc muốn đăng xuất?",
                    "Đăng xuất",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

            if (result !=
                MessageBoxResult.Yes)
            {
                return;
            }

            _socketService.Disconnect();

            LogoutRequested?.Invoke();
        }

        // =====================================================
        // ERROR
        // =====================================================

        private void SocketService_OnError(
            string message)
        {
            MessageBox.Show(
                message,
                "TCP Chat",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        // =====================================================
        // CLEANUP
        // =====================================================

        private void ContactListView_Unloaded(
            object sender,
            RoutedEventArgs e)
        {
            _socketService.OnOnlineUsersReceived -=
                SocketService_OnOnlineUsersReceived;

            _socketService.OnConversationListReceived -=
                SocketService_OnConversationListReceived;

            _socketService.OnUserStatusChanged -=
                SocketService_OnUserStatusChanged;

            _socketService.OnUserProfileChanged -=
                SocketService_OnUserProfileChanged;

            _socketService.OnAvatarUpdated -=
                SocketService_OnAvatarUpdated;

            _socketService.OnChatMessageReceived -=
                SocketService_OnChatMessageReceived;

            _socketService.OnError -=
                SocketService_OnError;
        }
    }

    // =========================================================
    // CHAT/CONTACT UI MODEL
    // =========================================================

    public class ChatItem :
        INotifyPropertyChanged
    {
        private bool _isOnline;

        private BitmapImage?
            _avatarImage;

        public int ConversationId
        {
            get;
            set;
        }

        public string UserId
        {
            get;
            set;
        } = string.Empty;

        public string Name
        {
            get;
            set;
        } = string.Empty;

        public string Username
        {
            get;
            set;
        } = string.Empty;

        public string LastMessage
        {
            get;
            set;
        } = string.Empty;

        public string Time
        {
            get;
            set;
        } = string.Empty;

        public string? AvatarData
        {
            get;
            set;
        }

        public BitmapImage? AvatarImage
        {
            get =>
                _avatarImage;

            set
            {
                if (_avatarImage == value)
                {
                    return;
                }

                _avatarImage =
                    value;

                OnPropertyChanged();
            }
        }

        public bool IsOnline
        {
            get =>
                _isOnline;

            set
            {
                if (_isOnline == value)
                {
                    return;
                }

                _isOnline =
                    value;

                OnPropertyChanged();

                OnPropertyChanged(
                    nameof(StatusText));
            }
        }

        public string StatusText =>
            IsOnline
                ? "Đang hoạt động"
                : "Ngoại tuyến";

        public event PropertyChangedEventHandler?
            PropertyChanged;

        private void OnPropertyChanged(
            [CallerMemberName]
            string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(
                    propertyName));
        }
    }
}