using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Client.Views
{
    public partial class ContactListView : UserControl
    {
        // Danh sách cuộc trò chuyện.
        // Hiện tại để trống, sau này dữ liệu thật từ Server
        // sẽ được thêm vào collection này.
        public ObservableCollection<ChatItem> Chats { get; }
            = new ObservableCollection<ChatItem>();

        public ContactListView()
        {
            InitializeComponent();

            // Bind ChatList với collection.
            // ObservableCollection sẽ tự thông báo cho WPF
            // khi có contact được thêm/xóa.
            ChatList.ItemsSource = Chats;

            UpdateEmptyState();
        }

        /// <summary>
        /// Thêm một contact/chat vào danh sách.
        /// Sau này ClientSocketService có thể gọi hàm này
        /// khi nhận dữ liệu thật từ Server.
        /// </summary>
        public void AddChat(ChatItem chat)
        {
            if (chat == null)
                return;

            Chats.Add(chat);

            UpdateEmptyState();
        }

        /// <summary>
        /// Xóa toàn bộ contact hiện tại.
        /// Hữu ích khi logout hoặc tải lại danh sách từ Server.
        /// </summary>
        public void ClearChats()
        {
            Chats.Clear();

            UpdateEmptyState();
        }

        /// <summary>
        /// Hiển thị EmptyPanel khi chưa có cuộc trò chuyện.
        /// Khi có dữ liệu thì hiển thị ChatList.
        /// </summary>
        private void UpdateEmptyState()
        {
            if (Chats.Count == 0)
            {
                EmptyPanel.Visibility = Visibility.Visible;
                ChatList.Visibility = Visibility.Collapsed;
            }
            else
            {
                EmptyPanel.Visibility = Visibility.Collapsed;
                ChatList.Visibility = Visibility.Visible;
            }
        }
    }

    public class ChatItem
    {
        public string Name { get; set; } = string.Empty;

        public string LastMessage { get; set; } = string.Empty;

        public string Time { get; set; } = string.Empty;
    }
}
