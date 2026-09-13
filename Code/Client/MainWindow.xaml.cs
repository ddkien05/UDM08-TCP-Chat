using ChatTCP.Client.Views;
using Client.Views;
using ChatTCP.Client.Networking;
using System.Windows;

namespace Client
{
    public partial class MainWindow :
        Window
    {
        public MainWindow()
        {
            InitializeComponent();

            ShowLogin();
        }

        // =====================================================
        // LOGIN
        // =====================================================

        private void ShowLogin()
        {
            var loginView =
                new LoginView();

            loginView.LoginSucceeded +=
                ShowContactList;

            MainContent.Content =
                loginView;
        }

        // =====================================================
        // CONTACT LIST
        // =====================================================

        private void ShowContactList(
            ClientSocketService socketService)
        {
            /*
             * Quan trọng:
             *
             * Dùng lại ClientSocketService từ LoginView.
             *
             * Nhờ đó ContactListView nhận được
             * USER_STATUS_NOTIFY realtime.
             */
            MainContent.Content =
                new ContactListView(
                    socketService);
        }

        // =====================================================
        // CHAT
        // =====================================================

        /*public void ShowChatView()
        {
            MainContent.Content =
                new ChatView();
        }*/
    }
}