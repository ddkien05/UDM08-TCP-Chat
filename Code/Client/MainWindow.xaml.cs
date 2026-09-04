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