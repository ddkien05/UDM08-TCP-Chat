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

namespace ChatTCP.Client.Views
{
    public partial class ContactListView : UserControl
    {
        public ObservableCollection<ChatItem> Chats { get; }
            = new ObservableCollection<ChatItem>();

        private readonly ICollectionView _chatView;
        private readonly string _avatarFolder;
        private readonly string _avatarFile;
        private readonly ClientSocketService? _socketService;

        /// <summary>
        /// Bắn khi người dùng chọn 1 liên hệ để mở màn hình chat.
        /// (targetUserId, targetDisplayName, isOnline)
        /// </summary>
        public event Action<string, string, bool>? ChatSelected;

        public ContactListView() : this(null)
        {
        }

        public ContactListView(ClientSocketService? socketService)
        {
            _socketService = socketService;
            InitializeComponent();

            // =========================
            // THƯ MỤC LƯU AVATAR
            // =========================

            _avatarFolder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "TCPChat",
                "Client");

            _avatarFile = Path.Combine(
                _avatarFolder,
                "my-avatar.png");

            // =========================
            // SETUP DANH SÁCH + SEARCH
            // =========================

            _chatView =
                CollectionViewSource.GetDefaultView(Chats);

            _chatView.Filter = FilterChat;

            // Chat có hoạt động gần nhất lên đầu danh sách, giống Messenger.
            _chatView.SortDescriptions.Add(
                new SortDescription(nameof(ChatItem.LastActivityAt), ListSortDirection.Descending));

            // "Live sorting": khi LastActivityAt của 1 item đổi (tin nhắn mới tới), item đó
            // tự nhảy lên đầu ngay lập tức mà không cần gọi Refresh() thủ công.
            if (_chatView is ICollectionViewLiveShaping liveShaping && liveShaping.CanChangeLiveSorting)
            {
                liveShaping.LiveSortingProperties.Add(nameof(ChatItem.LastActivityAt));
                liveShaping.IsLiveSorting = true;
            }

            ChatList.ItemsSource = _chatView;

            // =========================
            // DANH SÁCH LIÊN HỆ
            // =========================

            if (_socketService != null)
            {
                // Đã đăng nhập thật -> lấy danh sách user thật từ server
                _socketService.OnUserListReceived += HandleUserListReceived;
                _socketService.OnUserStatusChanged += HandleUserStatusChanged;
                _ = _socketService.RequestUserListAsync();

                // Preview tin nhắn + giờ real-time: lắng nghe ConversationStore (sống xuyên
                // suốt phiên) để cập nhật ngay khi có tin mới, kể cả tin gửi/nhận lúc ContactListView
                // này chưa tồn tại (vd. đang ở ChatView) vẫn được nạp lại đúng lúc list này mở ra.
                ConversationStore.Instance.Updated += HandleConversationUpdated;

                this.Unloaded += (_, _) =>
                {
                    _socketService.OnUserListReceived -= HandleUserListReceived;
                    _socketService.OnUserStatusChanged -= HandleUserStatusChanged;
                    ConversationStore.Instance.Updated -= HandleConversationUpdated;
                };
            }
            else
            {
                // Không có kết nối (vd: xem trước trong Designer) -> dùng dữ liệu mẫu
                LoadSampleData();
            }

            // =========================
            // LOAD AVATAR
            // =========================

            LoadSavedAvatar();

            UpdateEmptyState();
        }

        // =====================================================
        // NHẬN DANH SÁCH LIÊN HỆ THẬT TỪ SERVER
        // =====================================================

        private void HandleUserListReceived(ChatTCP.Common.Models.Packet<ChatTCP.Common.Models.UserListData> packet)
        {
            ClearChats();

            foreach (var user in packet.Data.Users)
            {
                var chat = new ChatItem
                {
                    Name = user.DisplayName,
                    Username = user.Username,
                    UserId = user.UserId,
                    IsOnline = user.IsOnline,
                    LastMessage = "Bắt đầu trò chuyện",
                    Time = string.Empty
                };

                // Nếu đã từng nhắn qua lại với người này trong phiên này (kể cả lúc
                // ContactListView chưa được tạo lại), nạp ngay tin nhắn gần nhất + giờ thật.
                var summary = ConversationStore.Instance.Get(user.UserId);
                if (summary != null)
                {
                    ApplyConversationSummary(chat, summary);
                }

                AddChat(chat);
            }
        }

        // =====================================================
        // CẬP NHẬT REAL-TIME: TIN NHẮN MỚI (GIỐNG MESSENGER)
        // =====================================================

        /// <summary>
        /// Bắn khi ConversationStore có tin nhắn mới (gửi đi hoặc nhận về) cho 1 liên hệ.
        /// Cập nhật ngay dòng chat tương ứng: preview nội dung, giờ, và badge chưa đọc —
        /// không cần gọi lại GET_USERS hay load lại toàn bộ danh sách.
        /// 
        /// LƯU Ý: tin nhắn ĐẾN có thể được ghi nhận từ luồng nền (ReceiveLoopAsync của
        /// ClientSocketService), nên bắt buộc phải quay về UI thread trước khi đụng vào
        /// ObservableCollection/Binding, nếu không WPF sẽ ném lỗi cross-thread.
        /// </summary>
        private void HandleConversationUpdated(string userId, ConversationSummary summary)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => HandleConversationUpdated(userId, summary)));
                return;
            }

            var chat = Chats.FirstOrDefault(c => c.UserId == userId);
            if (chat == null) return; // Người này chưa có trong danh sách liên hệ hiện tại

            ApplyConversationSummary(chat, summary);
        }

        private static void ApplyConversationSummary(ChatItem chat, ConversationSummary summary)
        {
            chat.LastMessage = summary.LastMessageIsMine
                ? $"Bạn: {summary.LastMessage}"
                : summary.LastMessage;

            chat.Time = FormatMessageTime(summary.LastMessageAt);
            chat.LastActivityAt = summary.LastMessageAt;
            chat.UnreadCount = summary.UnreadCount;
        }

        /// <summary>
        /// Định dạng giờ hiển thị kiểu Messenger: "HH:mm" nếu là hôm nay, "Hôm qua" nếu hôm qua,
        /// hoặc "dd/MM" nếu xa hơn.
        /// </summary>
        private static string FormatMessageTime(DateTime at)
        {
            if (at == DateTime.MinValue) return string.Empty;

            var today = DateTime.Now.Date;
            if (at.Date == today) return at.ToString("HH:mm");
            if (at.Date == today.AddDays(-1)) return "Hôm qua";
            return at.ToString("dd/MM");
        }

        // =====================================================
        // CẬP NHẬT REAL-TIME: TRẠNG THÁI ONLINE/OFFLINE
        // =====================================================

        private void HandleUserStatusChanged(ChatTCP.Common.Models.Packet<ChatTCP.Common.Models.UserStatusNotifyData> packet)
        {
            var chat = Chats.FirstOrDefault(c => c.UserId == packet.Data.UserId);
            if (chat == null) return;

            chat.IsOnline = packet.Data.Status == "ONLINE";
        }

        // =====================================================
        // DỮ LIỆU MẪU
        // =====================================================

        private void LoadSampleData()
        {
            AddChat(new ChatItem
            {
                Name = "Nguyễn Văn Nam",
                Username = "nam",
                LastMessage = "Hello, bạn đang làm gì vậy?",
                Time = "18:30"
            });

            AddChat(new ChatItem
            {
                Name = "Trần Minh Anh",
                Username = "minhanh",
                LastMessage = "Xin chào 👋",
                Time = "17:20"
            });

            AddChat(new ChatItem
            {
                Name = "Lê Hoàng Long",
                Username = "long123",
                LastMessage = "Tối nay học nhóm không?",
                Time = "16:45"
            });

            AddChat(new ChatItem
            {
                Name = "Phạm Thu Hà",
                Username = "thuha",
                LastMessage = "Ok nha 😄",
                Time = "15:12"
            });

            AddChat(new ChatItem
            {
                Name = "Đỗ Minh Quân",
                Username = "minhquan",
                LastMessage = "Gửi file cho mình nhé",
                Time = "14:05"
            });

            AddChat(new ChatItem
            {
                Name = "Nguyễn Thảo Vy",
                Username = "thaovy",
                LastMessage = "Cảm ơn bạn!",
                Time = "12:30"
            });

            AddChat(new ChatItem
            {
                Name = "Trần Quốc Huy",
                Username = "quochuy",
                LastMessage = "Mai gặp nhé.",
                Time = "10:15"
            });
        }

        // =====================================================
        // THÊM LIÊN HỆ / CHAT
        // =====================================================

        public void AddChat(ChatItem chat)
        {
            if (chat == null)
                return;

            Chats.Add(chat);

            _chatView.Refresh();

            UpdateEmptyState();
        }

        // =====================================================
        // XÓA TOÀN BỘ DANH SÁCH
        // =====================================================

        public void ClearChats()
        {
            Chats.Clear();

            _chatView.Refresh();

            UpdateEmptyState();
        }

        // =====================================================
        // TÌM KIẾM LIÊN HỆ
        // =====================================================

        private bool FilterChat(object item)
        {
            if (item is not ChatItem chat)
                return false;

            string keyword =
                SearchTextBox?.Text?.Trim()
                ?? string.Empty;

            // Không nhập gì -> hiện tất cả
            if (string.IsNullOrWhiteSpace(keyword))
                return true;

            // Tìm theo tên
            bool matchName =
                chat.Name.Contains(
                    keyword,
                    StringComparison.CurrentCultureIgnoreCase);

            // Tìm theo username
            bool matchUsername =
                chat.Username.Contains(
                    keyword,
                    StringComparison.CurrentCultureIgnoreCase);

            return matchName || matchUsername;
        }

        // =====================================================
        // CHỌN 1 LIÊN HỆ ĐỂ MỞ CHAT
        // =====================================================

        private void ChatList_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (ChatList.SelectedItem is ChatItem chat)
            {
                // Ưu tiên UserId thật (nếu có) để khớp với _clientMap phía server,
                // nếu không có (dữ liệu mẫu) thì tạm dùng Username.
                string targetId = string.IsNullOrWhiteSpace(chat.UserId)
                    ? chat.Username
                    : chat.UserId;

                // Mở chat -> coi như đã đọc hết, xóa badge thông báo ngay lập tức
                ConversationStore.Instance.MarkRead(targetId);

                ChatSelected?.Invoke(targetId, chat.Name, chat.IsOnline);

                // Bỏ chọn để có thể bấm lại cùng 1 contact và mở lại ChatView
                ChatList.SelectedItem = null;
            }
        }

        private void SearchTextBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            _chatView.Refresh();

            UpdateEmptyState();
        }

        // =====================================================
        // ĐỔI AVATAR
        // =====================================================

        private void ChangeAvatarButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Chọn ảnh đại diện",

                Filter =
                    "Ảnh (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|" +
                    "*.png;*.jpg;*.jpeg;*.bmp;*.webp",

                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                // Tạo folder nếu chưa tồn tại
                Directory.CreateDirectory(
                    _avatarFolder);

                // Load ảnh user vừa chọn
                BitmapImage bitmap =
                    LoadBitmap(dialog.FileName);

                // Lưu thành PNG
                using (FileStream stream =
                       File.Create(_avatarFile))
                {
                    var encoder =
                        new PngBitmapEncoder();

                    encoder.Frames.Add(
                        BitmapFrame.Create(bitmap));

                    encoder.Save(stream);
                }

                // Hiển thị avatar mới
                SetMyAvatar(_avatarFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Không thể đổi ảnh đại diện:\n" +
                    ex.Message,
                    "Avatar",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // =====================================================
        // LOAD AVATAR ĐÃ LƯU
        // =====================================================

        private void LoadSavedAvatar()
        {
            if (File.Exists(_avatarFile))
            {
                SetMyAvatar(_avatarFile);
            }
        }

        // =====================================================
        // HIỂN THỊ AVATAR
        // =====================================================

        private void SetMyAvatar(
            string imagePath)
        {
            BitmapImage bitmap =
                LoadBitmap(imagePath);

            MyAvatarBrush.ImageSource =
                bitmap;

            // Có avatar rồi thì ẩn icon mặc định
            DefaultAvatarIcon.Visibility =
                Visibility.Collapsed;
        }

        // =====================================================
        // LOAD BITMAP
        // =====================================================

        private static BitmapImage LoadBitmap(
            string path)
        {
            var bitmap =
                new BitmapImage();

            bitmap.BeginInit();

            // Load toàn bộ ảnh vào RAM
            // để tránh khóa file
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
        // EMPTY STATE
        // =====================================================

        private void UpdateEmptyState()
        {
            bool hasAnyChat =
                Chats.Count > 0;

            bool hasVisibleChat =
                _chatView
                    .Cast<object>()
                    .Any();

            // Có kết quả
            if (hasVisibleChat)
            {
                EmptyPanel.Visibility =
                    Visibility.Collapsed;

                ChatList.Visibility =
                    Visibility.Visible;

                return;
            }

            // Không có kết quả
            ChatList.Visibility =
                Visibility.Collapsed;

            EmptyPanel.Visibility =
                Visibility.Visible;

            // Có dữ liệu nhưng search không thấy
            if (hasAnyChat)
            {
                EmptyIcon.Text =
                    "🔎";

                EmptyMessage.Text =
                    "Không tìm thấy liên hệ";
            }
            else
            {
                // Không có dữ liệu nào
                EmptyIcon.Text =
                    "💬";

                EmptyMessage.Text =
                    "Chưa có cuộc trò chuyện";
            }
        }
    }

    // =========================================================
    // MODEL CHAT ITEM
    // =========================================================

    /// <summary>
    /// 1 dòng trong danh sách chat. Cài INotifyPropertyChanged để khi có tin nhắn mới tới
    /// (real-time từ ConversationStore) hoặc trạng thái Online/Offline đổi, dòng tương ứng
    /// tự cập nhật ngay trên giao diện mà KHÔNG cần load lại toàn bộ danh sách.
    /// </summary>
    public class ChatItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        private string _username = string.Empty;
        public string Username
        {
            get => _username;
            set => SetField(ref _username, value);
        }

        /// <summary>UserId thật của tài khoản (dùng để route CHAT_MSG). Để trống nếu chỉ là dữ liệu mẫu.</summary>
        public string UserId { get; set; }
            = string.Empty;

        private string _lastMessage = string.Empty;
        /// <summary>Nội dung tin nhắn gần nhất trong cuộc trò chuyện (kiểu preview như Messenger).</summary>
        public string LastMessage
        {
            get => _lastMessage;
            set => SetField(ref _lastMessage, value);
        }

        private bool _isOnline;
        /// <summary>Trạng thái online thật (dùng để tô màu chấm trạng thái trong danh sách).</summary>
        public bool IsOnline
        {
            get => _isOnline;
            set => SetField(ref _isOnline, value);
        }

        private string _time = string.Empty;
        /// <summary>Giờ hiển thị của tin nhắn gần nhất (vd "14:05", "Hôm qua", "18/09").</summary>
        public string Time
        {
            get => _time;
            set => SetField(ref _time, value);
        }

        private DateTime _lastActivityAt = DateTime.MinValue;
        /// <summary>Mốc thời gian thật của tin nhắn gần nhất, dùng để sắp xếp chat mới lên đầu (kiểu Messenger).</summary>
        public DateTime LastActivityAt
        {
            get => _lastActivityAt;
            set => SetField(ref _lastActivityAt, value);
        }

        private int _unreadCount;
        /// <summary>Số tin nhắn chưa đọc — hiện badge tròn giống thông báo chat mới của Messenger.</summary>
        public int UnreadCount
        {
            get => _unreadCount;
            set
            {
                if (SetField(ref _unreadCount, value))
                {
                    OnPropertyChanged(nameof(HasUnread));
                }
            }
        }

        /// <summary>True khi có tin nhắn chưa đọc — dùng để bôi đậm tên/preview giống Messenger.</summary>
        public bool HasUnread => UnreadCount > 0;

        public string? AvatarPath { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}