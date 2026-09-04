using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ChatTCP.Server.Services;

namespace ChatTCP.Server.Networking
{
    
    /// Server TCP chính. Phụ trách 2 việc:
    /// 1. Bind + Listen + Accept liên tục client mới (AcceptLoop), giao mỗi client
    ///    mới cho AuthHandler xử lý Login/Register trước khi coi là online.
    /// 2. Cung cấp cơ chế đọc/ghi message có ranh giới rõ ràng (ReadFrame/WriteFrame),
    ///    để module khác dùng khi đọc/gửi tin nhắn qua TCP stream.
 
    public class ChatServer
    {
        private const int ServerPort = 8888;
        private const int LengthPrefixSize = 4;

        private TcpListener _listener;
        private readonly AuthHandler _authHandler;
        private bool _isRunning;

        public ChatServer(AuthHandler authHandler)
        {
            _authHandler = authHandler;
        }

        /// Bind + Listen vào cổng 8888, sau đó bắt đầu Accept client mới liên tục trên Thread nền
        public void Start()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, ServerPort);
                _listener.Start(); // bên trong .NET tự thực hiện Bind() rồi Listen()
                _isRunning = true;

                Console.WriteLine($"[ChatServer] Server đã chạy, đang lắng nghe tại cổng {ServerPort}...");

                Thread acceptThread = new Thread(AcceptLoop);
                acceptThread.IsBackground = true;
                acceptThread.Start();
            }
            catch (SocketException ex)
            {
                Console.WriteLine("[ChatServer] Không thể khởi động server: " + ex.Message);
                throw;
            }
        }

        ///Dừng server, không nhận client mới nữa.
        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
            Console.WriteLine("[ChatServer] Server đã dừng.");
        }

        /// Vòng lặp Accept — bọc try-catch để 1 lỗi bất ngờ không làm chết cả server.
        private void AcceptLoop()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient newClient = _listener.AcceptTcpClient(); // đứng chờ tới khi có client mới kết nối

                    string ip = newClient.Client.RemoteEndPoint.ToString();
                    Console.WriteLine("[ChatServer] Có client mới kết nối: " + ip + " — đang chờ Login/Register...");

                    // Xử lý Login/Register trên Thread riêng, để không làm chậm việc Accept client tiếp theo.
                    // Bọc try-catch quanh Thread vì exception rơi ra ngoài Thread nền sẽ làm crash cả server.
                    Thread authThread = new Thread(() =>
                    {
                        try
                        {
                            _authHandler.Handle(newClient);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("[ChatServer] Lỗi không mong muốn khi xử lý client: " + ex.Message);
                        }
                    });
                    authThread.IsBackground = true;
                    authThread.Start();

                    // TODO: sau khi login thành công, module đọc tin nhắn liên tục (Khương)
                    // sẽ dùng ChatServer.ReadFrame(stream) để xử lý ranh giới message.
                }
                catch (SocketException)
                {
                    break; // xảy ra khi Stop() được gọi giữa lúc đang Accept
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[ChatServer] Lỗi không mong muốn trong AcceptLoop: " + ex.Message);
                }
            }
        }

        /// Gửi 1 message qua stream: viết 4 byte độ dài trước, rồi viết nội dung sau.
        /// TCP không tự tách được ranh giới từng gói tin, nên phải tự đóng khung như vậy.
 
        public static void WriteFrame(NetworkStream stream, byte[] payload)
        {
            try
            {
                byte[] lengthPrefix = BitConverter.GetBytes(payload.Length);
                stream.Write(lengthPrefix, 0, LengthPrefixSize);
                stream.Write(payload, 0, payload.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ChatServer] Lỗi khi gửi message: " + ex.Message);
                throw;
            }
        }

        /// Đọc đúng 1 message hoàn chỉnh: đọc đủ 4 byte độ dài trước, rồi đọc đủ
        /// số byte nội dung mà 4 byte đó khai báo — dù dữ liệu tới làm nhiều đợt
        /// vẫn ghép đủ mới trả về. Trả về null nếu client đã ngắt kết nối.
   
        public static byte[] ReadFrame(NetworkStream stream)
        {
            try
            {
                byte[] lengthPrefix = ReadExact(stream, LengthPrefixSize);
                if (lengthPrefix == null) return null; // client đã đóng kết nối

                int payloadLength = BitConverter.ToInt32(lengthPrefix, 0);

                if (payloadLength < 0 || payloadLength > 10 * 1024 * 1024) // chặn dữ liệu rác/bất thường > 10MB
                    throw new InvalidOperationException($"Độ dài message không hợp lệ: {payloadLength}");

                return ReadExact(stream, payloadLength);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ChatServer] Lỗi khi đọc message: " + ex.Message);
                return null;
            }
        }

        ///Đọc cho đủ đúng "count" byte, dù dữ liệu tới làm nhiều đợt nhỏ.
        private static byte[] ReadExact(NetworkStream stream, int count)
        {
            byte[] buffer = new byte[count];
            int offset = 0;

            while (offset < count)
            {
                int read = stream.Read(buffer, offset, count - offset);
                if (read == 0) return null; // stream đóng giữa chừng
                offset += read;
            }

            return buffer;
        }
    }
}