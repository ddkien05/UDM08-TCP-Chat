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

            var contactList = new ContactListView(_socketService);
            contactList.ChatSelected += ShowChatView;
            MainContent.Content = contactList;
        }

        public void ShowChatView(string targetUserId, string targetDisplayName)
        {
            if (_socketService == null) return;

            MainContent.Content = new ChatView(_socketService, targetUserId, targetDisplayName);
        }
    }
}