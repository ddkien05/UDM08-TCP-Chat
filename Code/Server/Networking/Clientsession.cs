using System.Net.Sockets;

namespace ChatTCP.Server.Networking
{
    /// Đại diện cho 1 client ĐÃ đăng nhập thành công (biết rõ là ai).
    /// Trước khi Login/Register xong, ta chỉ có TcpClient thô, chưa có gì trong này cả.
    /// AuthHandler tạo ra ClientSession này sau khi xác thực xong, rồi giao cho ClientManager quản lý.

    public class ClientSession
    {
        public TcpClient TcpClient { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; }
        public string DisplayName { get; set; }
    }
}