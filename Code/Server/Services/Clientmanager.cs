using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using ChatTCP.Common.Models;
using ChatTCP.Common.Protocol;
using ChatTCP.Server.Data;
using ChatTCP.Server.Networking;

namespace ChatTCP.Server.Services
{

    /// Quản lý danh sách client ĐANG ONLINE (đã login/register thành công).
    /// Giữ đồng thời 2 cấu trúc dữ liệu song song:
    /// - _sessions: List đầy đủ thông tin (UserId, Username, DisplayName, TcpClient) — dùng nội bộ.
    /// - _clientMap: ConcurrentDictionary&lt;string UserId, NetworkStream&gt; — đúng kiểu mà
    ///   MessageRouter.cs cần để gửi tin nhắn thẳng tới đúng người.

    public class ClientManager
    {
        private readonly List<ClientSession> _sessions = new List<ClientSession>();
        private readonly object _lock = new object();
        private readonly IUserRepository _userRepository;
        private readonly ConcurrentDictionary<string, ClientSession> _clientMap = new();

        public ClientManager(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        /// <summary>Đưa cho MessageRouter dùng — cả 2 lớp cùng tham chiếu tới 1 Dictionary duy nhất.
        /// Lưu ClientSession (không phải NetworkStream thô) để Router lấy được cả WriteLock,
        /// tránh 2 luồng ghi đồng thời vào cùng 1 socket làm hỏng khung tin.</summary>
        public ConcurrentDictionary<string, ClientSession> ClientMap => _clientMap;

        public void Add(ClientSession session)
        {
            lock (_lock)
            {
                _sessions.Add(session);
            }

            _clientMap[session.UserId.ToString()] = session;

            try
            {
                _userRepository.SetOnlineStatus(session.UserId, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ClientManager] Lỗi cập nhật online status: " + ex.Message);
            }

            Console.WriteLine($"[ClientManager] {session.Username} online. Tổng số hiện tại: {Count}");

            // Báo cho các client khác biết user này vừa online, để chấm trạng thái
            // trên danh sách chat của họ cập nhật ngay mà không cần load lại (real-time).
            BroadcastStatus(session, isOnline: true);
        }

        public void Remove(TcpClient client)
        {
            ClientSession session;
            lock (_lock)
            {
                session = _sessions.FirstOrDefault(s => s.TcpClient == client);
                if (session != null)
                    _sessions.Remove(session);
            }

            if (session != null)
            {
                _clientMap.TryRemove(session.UserId.ToString(), out _);
            }

            try
            {
                client.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ClientManager] Lỗi khi đóng socket: " + ex.Message);
            }

            if (session != null)
            {
                try
                {
                    _userRepository.SetOnlineStatus(session.UserId, false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[ClientManager] Lỗi cập nhật offline status: " + ex.Message);
                }

                Console.WriteLine($"[ClientManager] {session.Username} offline. Tổng số hiện tại: {Count}");

                // Báo cho các client khác biết user này vừa offline (thoát app / mất mạng /
                // bị HeartbeatMonitor gỡ) để cập nhật chấm trạng thái real-time.
                BroadcastStatus(session, isOnline: false);
            }
        }

        public ClientSession FindByUsername(string username)
        {
            lock (_lock)
            {
                return _sessions.FirstOrDefault(s => s.Username == username);
            }
        }

        public List<ClientSession> GetAll()
        {
            lock (_lock)
            {
                return new List<ClientSession>(_sessions);
            }
        }

        public List<string> GetOnlineDisplayNames()
        {
            lock (_lock)
            {
                return _sessions.Select(s => s.DisplayName).ToList();
            }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _sessions.Count;
                }
            }
        }

    
        // THÔNG BÁO ONLINE/OFFLINE REAL-TIME (USER_STATUS_NOTIFY)

        /// Đóng gói và gửi USER_STATUS_NOTIFY tới TẤT CẢ client khác (trừ chính người vừa đổi
        /// trạng thái). Chạy nền (Task.Run) vì Add/Remove đang được gọi đồng bộ từ AuthHandler,
        /// không muốn chặn luồng xử lý client hiện tại chỉ để chờ gửi thông báo cho người khác.
  
        private void BroadcastStatus(ClientSession session, bool isOnline)
        {
            var packet = new Packet<UserStatusNotifyData>
            {
                Type = "USER_STATUS_NOTIFY",
                Seq = 0,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new UserStatusNotifyData
                {
                    UserId = session.UserId.ToString(),
                    DisplayName = session.DisplayName,
                    Status = isOnline ? "ONLINE" : "OFFLINE",
                    LastSeen = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }
            };

            _ = BroadcastStatusAsync(packet, excludeUserId: session.UserId);
        }

        private async Task BroadcastStatusAsync(Packet<UserStatusNotifyData> packet, int excludeUserId)
        {
            var targets = GetAll().Where(s => s.UserId != excludeUserId).ToList();

            foreach (var target in targets)
            {
                try
                {
                    await target.WriteLock.WaitAsync();
                    try
                    {
                        await MessageProtocol.SendPacketAsync(target.TcpClient.GetStream(), packet);
                    }
                    finally
                    {
                        target.WriteLock.Release();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ClientManager] Lỗi gửi USER_STATUS_NOTIFY tới {target.Username}: {ex.Message}");
                }
            }
        }
    }
}