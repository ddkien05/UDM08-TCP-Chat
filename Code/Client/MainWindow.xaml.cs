using ChatTCP.Client.Networking;
using ChatTCP.Client.Views;
using System.Windows;

namespace Client
{
    public partial class MainWindow : Window
    {
        // Kết nối TCP duy nhất, được thiết lập lúc Login và dùng lại cho mọi màn hình sau đó.
        private ClientSocketService? _socketService;

        public MainWindow()
        {
            InitializeComponent();
            ShowLogin();
        }

        private void ShowLogin()
        {
            var loginView = new LoginView();
            loginView.LoginSucceeded += ShowContactList;
            loginView.RegisterRequested += ShowRegister;
            MainContent.Content = loginView;
        }

        private void ShowRegister()
        {
            var reg = new RegisterView();
            reg.RegistrationSucceeded += ShowLogin;
            reg.BackRequested += ShowLogin;
            MainContent.Content = reg;
        }

        private void ShowContactList(ClientSocketService socketService)
        {
            // Dùng lại đúng kết nối đã đăng nhập, KHÔNG tạo ClientSocketService mới
            _socketService = socketService;

            // Không còn ở trong cuộc chat nào -> tin nhắn mới tới sẽ được tính "chưa đọc" bình thường
            ConversationStore.Instance.ActiveChatUserId = null;

            var contactList = new ContactListView(_socketService);
            contactList.ChatSelected += ShowChatView;
            MainContent.Content = contactList;
        }

        public void ShowChatView(string targetUserId, string targetDisplayName, bool isOnline,
                          System.Windows.Media.ImageSource? avatar)
        {
            if (_socketService == null) return;

            ConversationStore.Instance.ActiveChatUserId = targetUserId;
            ConversationStore.Instance.MarkRead(targetUserId);

            var chatView = new ChatView(_socketService, targetUserId, targetDisplayName, isOnline, avatar);
            chatView.BackRequested += () => ShowContactList(_socketService);

            MainContent.Content = chatView;
        }
    }
}