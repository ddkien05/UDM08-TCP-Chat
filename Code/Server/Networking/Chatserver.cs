using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ChatTCP.Server.Services;

namespace ChatTCP.Server.Networking
{

    /// Server TCP chính: Bind + Listen + Accept liên tục client mới, giao mỗi client
    /// cho AuthHandler xử lý Login/Register trước khi coi là online.
    ///
    /// Không tự đóng khung message nữa (ReadFrame/WriteFrame cũ đã bỏ) — việc đó giờ
    /// giao hết cho ChatTCP.Common.Protocol.MessageProtocol của Thanh Thuý, để chỉ có
    /// đúng 1 nơi xử lý framing, tránh 2 kiểu đóng gói khác nhau tồn tại song song.
    public class ChatServer
    {
        private const int ServerPort = 8888;

        private TcpListener _listener;
        private readonly AuthHandler _authHandler;
        private bool _isRunning;

        public ChatServer(AuthHandler authHandler)
        {
            _authHandler = authHandler;
        }

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Any, ServerPort);
            _listener.Start();
            _isRunning = true;

            Console.WriteLine($"[ChatServer] Server da chay, dang lang nghe tai cong {ServerPort}...");

            Thread acceptThread = new Thread(AcceptLoop);
            acceptThread.IsBackground = true;
            acceptThread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
            Console.WriteLine("[ChatServer] Server da dung.");
        }

        private void AcceptLoop()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient newClient = _listener.AcceptTcpClient();

                    string ip = newClient.Client.RemoteEndPoint.ToString();
                    Console.WriteLine("[ChatServer] Co client moi ket noi: " + ip + " — dang cho Login/Register...");

                    Thread authThread = new Thread(() =>
                    {
                        try
                        {
                            // AuthHandler dùng JSON + async .
                            // Thread ở đây vẫn chạy đồng bộ, nên chờ luôn bằng GetAwaiter().GetResult().
                            _authHandler.HandleAsync(newClient).GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("[ChatServer] Loi khong mong muon xu li client: " + ex.Message);
                        }
                    });
                    authThread.IsBackground = true;
                    authThread.Start();
                }
                catch (SocketException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[ChatServer] Loi khong mong muon trong AcceptLoop: " + ex.Message);
                }
            }
        }
    }
}