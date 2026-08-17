using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Client.Views
{
    public partial class ContactListView : UserControl
    {
        public ContactListView()
        {
            InitializeComponent();
        }

        private void btnFakeData_Click(object sender, RoutedEventArgs e)
        {
            List<ChatItem> chats = new List<ChatItem>()
            {
                new ChatItem
                {
                    Name = "Nguyễn Văn A",
                    LastMessage = "Hello bạn!",
                    Time = "09:30"
                },

                new ChatItem
                {
                    Name = "Trần Thị B",
                    LastMessage = "Đang làm gì?",
                    Time = "10:15"
                },

                new ChatItem
                {
                    Name = "Lê Văn C",
                    LastMessage = "Mai học nhé.",
                    Time = "11:45"
                },

                new ChatItem
                {
                    Name = "Phạm Văn D",
                    LastMessage = "OK luôn.",
                    Time = "12:00"
                }
            };

            ChatList.ItemsSource = chats;

            EmptyPanel.Visibility = Visibility.Collapsed;
            ChatList.Visibility = Visibility.Visible;
        }
    }

    public class ChatItem
    {
        public string Name { get; set; }

        public string LastMessage { get; set; }

        public string Time { get; set; }
    }
}