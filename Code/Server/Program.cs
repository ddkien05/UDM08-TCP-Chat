using ChatTCP.Server.Data;
using ChatTCP.Server.Networking;
using ChatTCP.Server.Services;
using System;
namespace ChatTCP.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            ClientManager clientManager = new ClientManager();
            ChatServer server = new ChatServer(clientManager);

            server.Start();
            DbConnectionFactory.Initialize();
            Console.WriteLine("Nhấn Enter để dừng server...");
            Console.ReadLine();

            server.Stop();
        }
    }
}