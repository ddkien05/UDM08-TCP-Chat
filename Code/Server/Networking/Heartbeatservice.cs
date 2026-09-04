using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using ChatTCP.Server.Networking;

namespace ChatTCP.Server.Services
{
    public class HeartbeatService
    {
        private const int IntervalSeconds = 10; // 10 giây kiểm tra 1 lần

        private readonly ClientManager _clientManager;
        private bool _isRunning;

        public HeartbeatService(ClientManager clientManager)
        {
            _clientManager = clientManager;
        }

     
        public void Start()
        {
            _isRunning = true;

            Thread heartbeatThread = new Thread(Loop);
            heartbeatThread.IsBackground = true;
            heartbeatThread.Start();

            Console.WriteLine($"[HeartbeatService] Bắt đầu, kiểm tra mỗi {IntervalSeconds} giây.");
        }

        public void Stop()
        {
            _isRunning = false;
        }

        private void Loop()
        {
            while (_isRunning)
            {
                Thread.Sleep(IntervalSeconds * 1000);

                try
                {
                    var sessions = _clientManager.GetAll(); // lấy bản sao danh sách, an toàn khi Remove ngay trong lúc duyệt

                    foreach (var session in sessions)
                    {
                        if (!IsClientAlive(session.TcpClient))
                        {
                            Console.WriteLine($"[HeartbeatService] {session.Username} không phản hồi — coi như mất kết nối đột ngột.");
                            _clientManager.Remove(session.TcpClient);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 1 lỗi bất ngờ trong vòng kiểm tra không được làm chết cả vòng lặp Heartbeat
                    Console.WriteLine("[HeartbeatService] Lỗi không mong muốn trong Loop: " + ex.Message);
                }
            }
        }

 
        /// Kiểm tra 1 client còn sống không, bằng 2 bước:
        /// 1. Socket.Poll kiểu SelectRead + Available == 0 -> dấu hiệu kinh điển của
        ///    kết nối đã bị đóng phía bên kia mà server chưa nhận ra.
        /// 2. Thử ghi thật 1 gói "PING" xuống socket -> nếu ghi lỗi (SocketException/
        ///    IOException) nghĩa là kết nối đã chết thật sự.
   
        private bool IsClientAlive(TcpClient client)
        {
            try
            {
                Socket socket = client.Client;

                bool looksDisconnected = socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0;
                if (looksDisconnected)
                    return false;

                byte[] ping = Encoding.UTF8.GetBytes("PING\n");
                socket.Send(ping);

                return true;
            }
            catch (SocketException)
            {
                return false; // ghi thất bại -> chắc chắn đã mất kết nối
            }
            catch (ObjectDisposedException)
            {
                return false; // socket đã bị đóng từ trước (vd ClientManager.Remove gọi trước đó)
            }
        }
    }
}