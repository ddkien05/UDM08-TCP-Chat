using System;

namespace ChatTCP.Sever
{
    class Program
    {
        static void Main(string[] args)
        {
 
            ChatServer server = new ChatServer(clientManager);

            server.Start();

            Console.WriteLine("Nhan enter de dung sever...");
            Console.ReadLine();

            server.Stop();
        }
    }
}