using System.Windows;

using ChatTCP.Client.Networking;
using ChatTCP.Common.Models;

using Client.Views;

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

        private void ShowLogin()
        {
            var login =
                new LoginView();

            login.LoginSucceeded +=
                ShowContactList;

            MainContent.Content =
                login;
        }

        private void ShowContactList(
            ClientSocketService socketService,
            LoginResponseData currentUser)
        {
            var view =
                new ContactListView(
                    socketService,
                    currentUser);

            view.LogoutRequested +=
                ShowLogin;

            MainContent.Content =
                view;
        }
    }
}