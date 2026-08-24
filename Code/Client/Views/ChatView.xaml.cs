using System.Windows;
using System.Windows.Controls;

namespace ChatTCP.Client.Views
{
    public partial class ChatView : UserControl
    {
        public ChatView()
        {
            InitializeComponent();
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string content = MessageInputBox.Text;
            if (string.IsNullOrWhiteSpace(content)) return;

            // Giai đoạn 1: chỉ hiển thị demo, chưa gửi qua Socket thật
            MessageInputBox.Clear();
        }

        private void EmojiButton_Click(object sender, RoutedEventArgs e)
        {
            // Giai đoạn 3 sẽ làm Emoji Picker thật, giờ để trống
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {

        }
    }
}