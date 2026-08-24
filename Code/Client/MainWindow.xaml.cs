using ChatTCP.Client.Views;
using Client.Views;
using System.Windows;

namespace Client
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            ShowLogin();
        }

        private void ShowLogin()
        {
            var loginView = new LoginView();

            loginView.LoginSucceeded += ShowContactList;

            MainContent.Content = loginView;
        }

        private void ShowContactList()
        {
            MainContent.Content = new ContactListView();
        }

        public void ShowChatView()
        {
            MainContent.Content = new ChatView();
        }
    }
}