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
                Console.WriteLine($"Lỗi khởi tạo Database: {ex.Message}");
                return;
            }

            IUserRepository userRepository = new UserRepository();

            ClientManager clientManager =
                new ClientManager(userRepository);

            AuthHandler authHandler =
                new AuthHandler(userRepository, clientManager);

            ChatServer server =
                new ChatServer(authHandler);

            HeartbeatMonitor heartbeatMonitor =
                new HeartbeatMonitor(clientManager);

            IMessageRepository messageRepository =
                new MessageRepository();

            MessageRouter messageRouter =
                new MessageRouter(
                    clientManager.ClientMap,
                    messageRepository);

            server.Start();
            heartbeatMonitor.Start();

            Console.WriteLine("Nhấn Enter để dừng server...");
            Console.ReadLine();

            heartbeatMonitor.Stop();
            server.Stop();
        }
    }
}