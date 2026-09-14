using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ChatTCP.Server.Services;

namespace ChatTCP.Server.Networking
{
    public class ChatServer
    {
        private const int PORT = 8888;

        private TcpListener listener;
        private AuthHandler authHandler;
        private bool isRunning;

        public ChatServer(AuthHandler authHandler)
        {
            this.authHandler = authHandler;
        }

        // Mở cổng 8888 và bắt đầu nhận client mới
        public void Start()
        {
            listener = new TcpListener(IPAddress.Any, PORT);
            listener.Start();
            isRunning = true;

            Console.WriteLine("Server đã chạy, đang lắng nghe tại cổng " + PORT + "...");

            Thread acceptThread = new Thread(AcceptLoop);
            acceptThread.IsBackground = true;
            acceptThread.Start();
        }

        public void Stop()
        {
            isRunning = false;
            listener.Stop();
            Console.WriteLine("Server đã dừng.");
        }

        // Vòng lặp: có client mới kết nối tới là nhận ngay
        private void AcceptLoop()
        {
            while (isRunning)
            {
                try
                {
                    TcpClient newClient = listener.AcceptTcpClient();
                    Console.WriteLine("Có client mới kết nối: " + newClient.Client.RemoteEndPoint);

                    // Xử lý login/register trên Thread riêng để không làm chậm client tiếp theo
                    Thread thread = new Thread(() =>
                    {
                        try
                        {
                            authHandler.Handle(newClient);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Lỗi khi xử lý client: " + ex.Message);
                        }
                    });
                    thread.IsBackground = true;
                    thread.Start();
                }
                catch (SocketException)
                {
                    break; // xảy ra khi Stop() được gọi
                }
            }
        }

        // Gửi 1 tin nhắn: ghi 4 byte độ dài trước, nội dung sau
        public static void WriteFrame(NetworkStream stream, byte[] data)
        {
            byte[] lengthBytes = BitConverter.GetBytes(data.Length);
            stream.Write(lengthBytes, 0, 4);
            stream.Write(data, 0, data.Length);
        }

        // Đọc 1 tin nhắn: đọc đủ 4 byte độ dài, rồi đọc đủ nội dung
        public static byte[] ReadFrame(NetworkStream stream)
        {
            byte[] lengthBytes = ReadExact(stream, 4);
            if (lengthBytes == null)
                return null;

            int length = BitConverter.ToInt32(lengthBytes, 0);
            return ReadExact(stream, length);
        }

        // Đọc cho đủ đúng số byte cần, vì TCP có thể gửi rời rạc nhiều lần
        private static byte[] ReadExact(NetworkStream stream, int count)
        {
            byte[] buffer = new byte[count];
            int total = 0;

            while (total < count)
            {
                int read = stream.Read(buffer, total, count - total);
                if (read == 0)
                    return null; // client đã đóng kết nối

                total += read;
            }

            return buffer;
        }
    }
}