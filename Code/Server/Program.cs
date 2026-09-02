using System;
using ChatTCP.Server.Networking;
using ChatTCP.Server.Services;
namespace ChatTCP.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            ClientManager clientManager = new ClientManager();
            ChatServer server = new ChatServer(clientManager);

            server.Start();

            Console.WriteLine("Nhấn Enter để dừng server...");
            Console.ReadLine();

            server.Stop();
        }
    }
}