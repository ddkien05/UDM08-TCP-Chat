using System;
using ChatTCP.Server.Data;
using ChatTCP.Server.Networking;
using ChatTCP.Server.Services;

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
                Console.WriteLine("Lỗi khởi tạo Database: " + ex.Message);
                return;
            }

            UserRepository userRepository = new UserRepository();
            ClientManager clientManager = new ClientManager(userRepository);
            AuthHandler authHandler = new AuthHandler(userRepository, clientManager);
            ChatServer server = new ChatServer(authHandler);
          

            server.Start();
      

            Console.WriteLine("Nhấn Enter để dừng server...");
            Console.ReadLine();

    
            server.Stop();
        }
    }
}