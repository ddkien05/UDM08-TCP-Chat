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
            Console.WriteLine("Them client . Tong so client hien tai: " + Count);
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

            Console.WriteLine("Xoa client . Tong so client hien tai la: " + Count);
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