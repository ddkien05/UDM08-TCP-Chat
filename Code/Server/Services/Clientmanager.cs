using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
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
        private readonly ConcurrentDictionary<string, NetworkStream> _clientMap = new();

        public ClientManager(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        /// <summary>Đưa cho MessageRouter dùng — cả 2 lớp cùng tham chiếu tới 1 Dictionary duy nhất.</summary>
        public ConcurrentDictionary<string, NetworkStream> ClientMap => _clientMap;

        public void Add(ClientSession session)
        {
            lock (_lock)
            {
                _sessions.Add(session);
            }

            _clientMap[session.UserId.ToString()] = session.TcpClient.GetStream();

            try
            {
                _userRepository.SetOnlineStatus(session.UserId, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ClientManager] Lỗi cập nhật online status: " + ex.Message);
            }

            Console.WriteLine($"[ClientManager] {session.Username} online. Tổng số hiện tại: {Count}");
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
    }
}