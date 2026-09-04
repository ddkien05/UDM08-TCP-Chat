using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using ChatTCP.Server.Data;
using ChatTCP.Server.Networking;

namespace ChatTCP.Server.Services
{
  
    /// Quản lý danh sách client ĐANG ONLINE (đã login/register thành công).
    /// Biết rõ mỗi kết nối là user nào (UserId/Username), không còn là danh sách TcpClient "vô danh" như trước nữa.
  
    public class ClientManager
    {
        private readonly List<ClientSession> _sessions = new List<ClientSession>();
        private readonly object _lock = new object();
        private readonly IUserRepository _userRepository;

        public ClientManager(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        ///Thêm 1 session mới — gọi sau khi AuthHandler xác thực login/register thành công
        public void Add(ClientSession session)
        {
            lock (_lock)
            {
                _sessions.Add(session);
            }

            try
            {
                _userRepository.SetOnlineStatus(session.UserId, true); // đồng bộ trạng thái online xuống Database
            }
            catch (Exception ex)
            {
                // Không để lỗi ghi Database làm mất session đã thêm vào RAM, chỉ log lại.
                Console.WriteLine("[ClientManager] Lỗi cập nhật online status: " + ex.Message);
            }

            Console.WriteLine($"[ClientManager] {session.Username} online. Tổng số hiện tại: {Count}");
        }

        ///Xoá session khi client ngắt kết nối, đóng socket an toàn và đánh dấu offline trong Database.
        public void Remove(TcpClient client)
        {
            ClientSession session;
            lock (_lock)
            {
                session = _sessions.FirstOrDefault(s => s.TcpClient == client);
                if (session != null)
                    _sessions.Remove(session);
            }

            try
            {
                client.Close();
            }
            catch (Exception ex)
            {
                // socket có thể đã đóng từ phía client rồi, chỉ log lại chứ không để văng lỗi ra ngoài
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

        ///Tìm session theo Username — dùng khi muốn gửi tin nhắn riêng tới 1 người cụ thể.
        public ClientSession FindByUsername(string username)
        {
            lock (_lock)
            {
                return _sessions.FirstOrDefault(s => s.Username == username);
            }
        }

        ///Lấy toàn bộ session đang online — dùng cho broadcast sau này.
        public List<ClientSession> GetAll()
        {
            lock (_lock)
            {
                return new List<ClientSession>(_sessions);
            }
        }

        ///Lấy danh sách tên hiển thị của những người đang online — dùng để hiện lên GUI.
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