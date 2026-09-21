using System;
using System.Net.Sockets;
using System.Threading;
using ChatTCP.Server.Networking;

namespace ChatTCP.Server.Services
{
    /// <summary>Định kỳ kiểm tra client nào bị rớt mạng đột ngột, tự động gỡ khỏi danh sách online.</summary>
    public class HeartbeatMonitor
    {
        private const int CheckIntervalMs = 5000;

        private readonly ClientManager _clientManager;
        private bool _isRunning;

        public HeartbeatMonitor(ClientManager clientManager)
        {
            _clientManager = clientManager;
        }

        public void Start()
        {
            _isRunning = true;

            Thread monitorThread = new Thread(MonitorLoop);
            monitorThread.IsBackground = true;
            monitorThread.Start();

            Console.WriteLine("[HeartbeatMonitor] Bắt đầu theo dõi kết nối, kiểm tra mỗi " + (CheckIntervalMs / 1000) + " giây.");
        }

        public void Stop()
        {
            _isRunning = false;
        }

        private void MonitorLoop()
        {
            while (_isRunning)
            {
                try
                {
                    CheckAllSessions();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[HeartbeatMonitor] Lỗi không mong muốn: " + ex.Message);
                }

                Thread.Sleep(CheckIntervalMs);
            }
        }

        private void CheckAllSessions()
        {
            var sessions = _clientManager.GetAll();

            foreach (var session in sessions)
            {
                if (IsDisconnected(session.TcpClient))
                {
                    Console.WriteLine($"[HeartbeatMonitor] Phát hiện {session.Username} mất kết nối đột ngột, đang gỡ khỏi danh sách online...");
                    _clientManager.Remove(session.TcpClient);
                }
            }
        }

        private static bool IsDisconnected(TcpClient client)
        {
            try
            {
                Socket socket = client.Client;
                bool pollResult = socket.Poll(0, SelectMode.SelectRead);
                bool noDataAvailable = socket.Available == 0;
                return pollResult && noDataAvailable;
            }
            catch (SocketException)
            {
                return true;
            }
            catch (ObjectDisposedException)
            {
                return true;
            }
        }
    }
}