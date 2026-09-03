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
            try
            {
                DbConnectionFactory.Initialize();
                Console.WriteLine("Khởi tạo Database thành công!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khởi tạo Database: {ex.Message}");
                return; // Nếu lỗi DB thì dừng luôn, không chạy Server nữa
            }

            ClientManager clientManager = new ClientManager();
            ChatServer server = new ChatServer(clientManager);

            server.Start();
            Console.WriteLine("Nhấn Enter để dừng server...");
            Console.ReadLine();

            server.Stop();
        }
    }
}