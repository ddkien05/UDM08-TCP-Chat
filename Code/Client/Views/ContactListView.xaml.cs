using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Client.Views
{
    public partial class ContactListView : UserControl
    {
        public ObservableCollection<ChatItem> Chats { get; }
            = new ObservableCollection<ChatItem>();

        private readonly ICollectionView _chatView;
        private readonly string _avatarFolder;
        private readonly string _avatarFile;

        public ContactListView()
        {
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

            ChatList.ItemsSource = _chatView;

            // =========================
            // DỮ LIỆU MẪU
            // =========================

            LoadSampleData();

            // =========================
            // LOAD AVATAR
            // =========================

            LoadSavedAvatar();

            UpdateEmptyState();
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

    public class ChatItem
    {
        public string Name { get; set; }
            = string.Empty;

        public string Username { get; set; }
            = string.Empty;

        public string LastMessage { get; set; }
            = string.Empty;

        public string Time { get; set; }
            = string.Empty;

        public string? AvatarPath { get; set; }
    }
}