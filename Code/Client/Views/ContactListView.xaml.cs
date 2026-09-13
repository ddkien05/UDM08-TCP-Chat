using Microsoft.Win32;

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

using ChatTCP.Client.Networking;
using ChatTCP.Client.Views;
using ChatTCP.Common.Models;

namespace Client.Views
{
    public partial class ContactListView : UserControl
    {
        // =====================================================
        // CHAT DATA
        // =====================================================

        public ObservableCollection<ChatItem> Chats { get; }
            = new ObservableCollection<ChatItem>();

        private readonly ICollectionView _chatView;

        private readonly string _avatarFolder;
        private readonly string _avatarFile;

        private readonly ClientSocketService? _socketService;

        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        public ContactListView()
            : this(null)
        {
        }

        public ContactListView(
            ClientSocketService? socketService)
        {
            InitializeComponent();

            _socketService = socketService;

            // =================================================
            // SOCKET STATUS EVENT
            // =================================================

            if (_socketService != null)
            {
                _socketService.OnUserStatusChanged +=
                    SocketService_OnUserStatusChanged;

                Unloaded +=
                    ContactListView_Unloaded;
            }

            // =================================================
            // AVATAR STORAGE
            // =================================================

            _avatarFolder =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "TCPChat",
                    "Client");

            _avatarFile =
                Path.Combine(
                    _avatarFolder,
                    "my-avatar.png");

            // =================================================
            // CHAT COLLECTION
            // =================================================

            _chatView =
                CollectionViewSource
                    .GetDefaultView(Chats);

            _chatView.Filter =
                FilterChat;

            ChatList.ItemsSource =
                _chatView;

            // =================================================
            // TEMP DATA
            // =================================================

            LoadSampleData();

            // =================================================
            // MY AVATAR
            // =================================================

            LoadSavedAvatar();

            UpdateEmptyState();
        }

        // =====================================================
        // SAMPLE DATA
        // =====================================================

        private void LoadSampleData()
        {
            AddChat(
                new ChatItem
                {
                    UserId = "1",
                    Name = "Nguyễn Văn Nam",
                    Username = "nam",
                    IsOnline = true,
                    LastMessage = "Hello, bạn đang làm gì vậy?",
                    Time = "18:30"
                });

            AddChat(
                new ChatItem
                {
                    UserId = "2",
                    Name = "Trần Minh Anh",
                    Username = "minhanh",
                    IsOnline = false,
                    LastMessage = "Xin chào 👋",
                    Time = "17:20"
                });

            AddChat(
                new ChatItem
                {
                    UserId = "3",
                    Name = "Lê Hoàng Long",
                    Username = "long123",
                    IsOnline = true,
                    LastMessage = "Tối nay học nhóm không?",
                    Time = "16:45"
                });

            AddChat(
                new ChatItem
                {
                    UserId = "4",
                    Name = "Phạm Thu Hà",
                    Username = "thuha",
                    IsOnline = false,
                    LastMessage = "Ok nha 😄",
                    Time = "15:12"
                });

            AddChat(
                new ChatItem
                {
                    UserId = "5",
                    Name = "Đỗ Minh Quân",
                    Username = "minhquan",
                    IsOnline = false,
                    LastMessage = "Gửi file cho mình nhé",
                    Time = "14:05"
                });

            AddChat(
                new ChatItem
                {
                    UserId = "6",
                    Name = "Nguyễn Thảo Vy",
                    Username = "thaovy",
                    IsOnline = true,
                    LastMessage = "Cảm ơn bạn!",
                    Time = "12:30"
                });

            AddChat(
                new ChatItem
                {
                    UserId = "7",
                    Name = "Trần Quốc Huy",
                    Username = "quochuy",
                    IsOnline = false,
                    LastMessage = "Mai gặp nhé.",
                    Time = "10:15"
                });
        }

        // =====================================================
        // ADD CHAT
        // =====================================================

        public void AddChat(
            ChatItem chat)
        {
            if (chat == null)
            {
                return;
            }

            Chats.Add(chat);

            _chatView.Refresh();

            UpdateEmptyState();
        }

        // =====================================================
        // CLEAR CHAT
        // =====================================================

        public void ClearChats()
        {
            Chats.Clear();

            _chatView.Refresh();

            ChatContentHost.Content = null;

            NoChatSelectedPanel.Visibility =
                Visibility.Visible;

            UpdateEmptyState();
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

            bool matchName =
                chat.Name.Contains(
                    keyword,
                    StringComparison.CurrentCultureIgnoreCase);

            bool matchUsername =
                chat.Username.Contains(
                    keyword,
                    StringComparison.CurrentCultureIgnoreCase);

            return matchName ||
                   matchUsername;
        }

        private void SearchTextBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            _chatView.Refresh();

            UpdateEmptyState();
        }

        // =====================================================
        // SELECT CHAT
        // =====================================================

        private void ChatList_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (ChatList.SelectedItem
                is not ChatItem selectedChat)
            {
                return;
            }

            var chatView =
                new ChatView(
                    selectedChat,
                    _socketService);

            ChatContentHost.Content =
                chatView;

            NoChatSelectedPanel.Visibility =
                Visibility.Collapsed;
        }

        // =====================================================
        // AVATAR
        // =====================================================

        private void ChangeAvatarButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var dialog =
                new OpenFileDialog
                {
                    Title =
                        "Chọn ảnh đại diện",

                    Filter =
                        "Ảnh (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|" +
                        "*.png;*.jpg;*.jpeg;*.bmp;*.webp",

                    Multiselect =
                        false
                };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(
                    _avatarFolder);

                BitmapImage bitmap =
                    LoadBitmap(
                        dialog.FileName);

                using FileStream stream =
                    File.Create(
                        _avatarFile);

                var encoder =
                    new PngBitmapEncoder();

                encoder.Frames.Add(
                    BitmapFrame.Create(
                        bitmap));

                encoder.Save(
                    stream);

                SetMyAvatar(
                    _avatarFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Không thể đổi ảnh đại diện:\n"
                    + ex.Message,

                    "Avatar",

                    MessageBoxButton.OK,

                    MessageBoxImage.Error);
            }
        }

        private void LoadSavedAvatar()
        {
            if (File.Exists(
                    _avatarFile))
            {
                SetMyAvatar(
                    _avatarFile);
            }
        }

        private void SetMyAvatar(
            string imagePath)
        {
            BitmapImage bitmap =
                LoadBitmap(
                    imagePath);

            MyAvatarBrush.ImageSource =
                bitmap;

            DefaultAvatarIcon.Visibility =
                Visibility.Collapsed;
        }

        private static BitmapImage LoadBitmap(
            string path)
        {
            var bitmap =
                new BitmapImage();

            bitmap.BeginInit();

            bitmap.CacheOption =
                BitmapCacheOption.OnLoad;

            bitmap.UriSource =
                new Uri(
                    path,
                    UriKind.Absolute);

            bitmap.EndInit();

            bitmap.Freeze();

            return bitmap;
        }

        // =====================================================
        // ONLINE STATUS
        // =====================================================

        private void SocketService_OnUserStatusChanged(
            Packet<UserStatusNotifyData> packet)
        {
            if (packet?.Data == null)
            {
                return;
            }

            ApplyUserStatus(
                packet.Data);
        }

        public void ApplyUserStatus(
            UserStatusNotifyData status)
        {
            if (status == null)
            {
                return;
            }

            bool isOnline =
                string.Equals(
                    status.Status,
                    "ONLINE",
                    StringComparison.OrdinalIgnoreCase);

            ChatItem? chat =
                null;

            if (!string.IsNullOrWhiteSpace(
                    status.UserId))
            {
                chat =
                    Chats.FirstOrDefault(
                        x =>
                            x.UserId ==
                            status.UserId);
            }

            if (chat == null)
            {
                chat =
                    Chats.FirstOrDefault(
                        x =>
                            string.Equals(
                                x.Name,
                                status.DisplayName,
                                StringComparison.CurrentCultureIgnoreCase));
            }

            if (chat == null)
            {
                return;
            }

            chat.IsOnline =
                isOnline;
        }

        // =====================================================
        // EMPTY STATE
        // =====================================================

        private void UpdateEmptyState()
        {
            bool hasVisibleChat =
                _chatView
                    .Cast<object>()
                    .Any();

            if (hasVisibleChat)
            {
                EmptyPanel.Visibility =
                    Visibility.Collapsed;

                ChatList.Visibility =
                    Visibility.Visible;

                return;
            }

            ChatList.Visibility =
                Visibility.Collapsed;

            EmptyPanel.Visibility =
                Visibility.Visible;

            if (Chats.Count > 0)
            {
                EmptyIconImage.Source =
                    new BitmapImage(
                        new Uri(
                            "pack://application:,,,/Assets/Icon/icon_search.png",
                            UriKind.Absolute));

                EmptyMessage.Text =
                    "Không tìm thấy liên hệ";
            }
            else
            {
                EmptyIconImage.Source =
                    new BitmapImage(
                        new Uri(
                            "pack://application:,,,/Assets/Icon/icon_chat.png",
                            UriKind.Absolute));

                EmptyMessage.Text =
                    "Chưa có cuộc trò chuyện";
            }
        }

        // =====================================================
        // CLEAN EVENT
        // =====================================================

        private void ContactListView_Unloaded(
            object sender,
            RoutedEventArgs e)
        {
            if (_socketService != null)
            {
                _socketService.OnUserStatusChanged -=
                    SocketService_OnUserStatusChanged;
            }
        }
    }

    // =========================================================
    // CHAT ITEM
    // =========================================================

    public class ChatItem :
        INotifyPropertyChanged
    {
        private bool _isOnline;

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

        public string? AvatarPath
        {
            get;
            set;
        }

        public bool IsOnline
        {
            get => _isOnline;

            set
            {
                if (_isOnline == value)
                {
                    return;
                }

                _isOnline = value;

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