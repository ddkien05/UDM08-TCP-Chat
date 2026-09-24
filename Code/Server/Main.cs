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
                Console.WriteLine("Khoi tao Database thanh cong!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Loi khoi tao Database: {ex.Message}");
                return;
            }

            IUserRepository userRepository = new UserRepository();

            ClientManager clientManager =
                new ClientManager(userRepository);

            MessageRouter messageRouter =
              new MessageRouter(clientManager.ClientMap);

            AuthHandler authHandler =
                new AuthHandler(userRepository, clientManager, messageRouter);

            ChatServer server =
                new ChatServer(authHandler);

            HeartbeatMonitor heartbeatMonitor =
                new HeartbeatMonitor(clientManager);

            server.Start();
            heartbeatMonitor.Start();

            Console.WriteLine("Nhan Enter de dung server...");
            Console.ReadLine();

            heartbeatMonitor.Stop();
            server.Stop();
        }
    }
}