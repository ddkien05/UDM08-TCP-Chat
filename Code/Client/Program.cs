using System;
using System.Net.Sockets;
using System.Text;

namespace ChatTCP.Client
{
  // Test ket noi Server
    class Program
    {
        static void Main(string[] args)
        {
            TcpClient client = new TcpClient();
            client.Connect("127.0.0.1", 8888);
            Console.WriteLine("Da ket noi toi server, go exit de thoat:");

            NetworkStream stream = client.GetStream();

            while (true)
            {
                string input = Console.ReadLine();
                if (input == "exit") break;

                byte[] data = Encoding.UTF8.GetBytes(input);
                stream.Write(data, 0, data.Length);
            }

            client.Close();
        }
    }
}