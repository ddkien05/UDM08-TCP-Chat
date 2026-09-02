using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace ChatTCP.Server.Services
{

    /// Quản lý danh sách các client đang kết nối tới server.

    public class ClientManager
    {
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private readonly object _lock = new object(); // khoá để nhiều Thread không cùng sửa _clients 1 lúc

        ///Thêm 1 client mới vào danh sách=
        public void Add(TcpClient client)
        {
            lock (_lock)
            {
                _clients.Add(client);
            }
            Console.WriteLine("[ClientManager] Thêm client. Tổng số hiện tại: " + Count);
        }

        /// Xoá client khỏi danh sách và đóng socket an toàn khi client rời đi
        public void Remove(TcpClient client)
        {
            lock (_lock)
            {
                _clients.Remove(client);
            }

            try
            {
                client.Close();
            }
            catch (Exception ex)
            {
                // client có thể đã tự đóng trước rồi, chỉ log lại chứ không để văng lỗi ra ngoài
                Console.WriteLine("[ClientManager] Lỗi khi đóng socket: " + ex.Message);
            }

            Console.WriteLine("[ClientManager] Xoá client. Tổng số hiện tại: " + Count);
        }

        ///Lấy toàn bộ client đang online — dùng cho broadcast sau này.
        public List<TcpClient> GetAll()
        {
            lock (_lock)
            {
                return new List<TcpClient>(_clients);
            }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _clients.Count;
                }
            }
        }
    }
}