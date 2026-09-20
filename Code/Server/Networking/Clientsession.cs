using System.Net.Sockets;
using System.Threading;

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

        /// <summary>
        /// Khóa ghi (write lock) riêng cho stream của session này.
        /// QUAN TRỌNG: NetworkStream không an toàn khi bị ghi đồng thời từ nhiều luồng
        /// (ví dụ: luồng của chính client này tự gửi USER_LIST, VÀ CÙNG LÚC luồng của
        /// MessageRouter đang route CHAT_MSG của người khác tới client này).
        /// Ghi chồng chéo sẽ làm hỏng khung tin (length-prefix), khiến phía nhận đọc lỗi
        /// và bị ngắt kết nối đột ngột. Mọi nơi ghi vào TcpClient.GetStream() của session này
        /// đều phải WaitAsync()/Release() qua khóa này trước.
        /// </summary>
        public SemaphoreSlim WriteLock { get; } = new SemaphoreSlim(1, 1);
    }
}