using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace ChatTCP.Sever
{
 
    public class ClientManager
    {
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private readonly object _lock = new object(); 

        public void Add(TcpClient client)
        {
            lock (_lock)
            {
                _clients.Add(client);
            }
            Console.WriteLine("[ClientManager] Thêm client. Tổng số hiện tại: " + Count);
        }

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
            catch
            {
                
            }

            Console.WriteLine("[ClientManager] Xoá client. Tổng số hiện tại: " + Count);
        }

      
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