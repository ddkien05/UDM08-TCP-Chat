using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ChatTCP.Server
{

    public class ChatServer
    {
        private TcpListener _listener;
        private readonly ClientManager _clientManager;
        private bool _isRunning;

        public ChatServer(ClientManager clientManager)
        {
            _clientManager = clientManager;
        }

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Any, 8888);
            _listener.Start();
            _isRunning = true;

            Console.WriteLine("Chat server dang chay va o cong 8888");

            Thread acceptThread = new Thread(AcceptLoop);
            acceptThread.IsBackground = true;
            acceptThread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
            _listener.Stop();
            Console.WriteLine("Server da dung.");
        }

        private void AcceptLoop()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient newClient = _listener.AcceptTcpClient();

                    string ip = newClient.Client.RemoteEndPoint.ToString();
                    Console.WriteLine("co client moi ket noi voi dia chi ip la: " + ip);

                    _clientManager.Add(newClient);


                }
                catch (SocketException)
                {
                    break;
                }
            }
        }
    }
}