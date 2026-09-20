
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
              
                // 1. KHỞI TẠO DATABASE
      

                DbConnectionFactory.Initialize();

                Console.WriteLine(
                    "Khởi tạo Database thành công!");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Lỗi khởi tạo Database: {ex.Message}");

                return;
            }

     
            // 2. REPOSITORY


            IUserRepository userRepository =
                new UserRepository();

            IMessageRepository messageRepository =
                new MessageRepository();


            // 3. CLIENT MANAGER
        

            ClientManager clientManager =
                new ClientManager(userRepository);

            // 4. MESSAGE ROUTER
       

            MessageRouter messageRouter =
                new MessageRouter(
                    clientManager.ClientMap,
                    messageRepository);

            // 5. AUTH HANDLER
  

            AuthHandler authHandler =
                new AuthHandler(
                    userRepository,
                    clientManager,
                    messageRouter);

            // 6. CHAT SERVER
     

            ChatServer server =
                new ChatServer(authHandler);

            // 7. HEARTBEAT
       

            HeartbeatMonitor heartbeatMonitor =
                new HeartbeatMonitor(clientManager);

    
            // 8. START SERVER
     

            server.Start();

            heartbeatMonitor.Start();

            Console.WriteLine(
                "Nhấn Enter để dừng server...");

            Console.ReadLine();


            // 9. STOP SERVER


            heartbeatMonitor.Stop();

            server.Stop();
        }
    }
}

