using System.Net.Sockets;
using System.Text;
using TcpChat.Common.Constants;

class Chatclient
{
    private const string SERVER_IP = "127.0.0.1";
    private const int SERVER_PORT = 9999;

    private static TcpClient client;

    public static async Task Main()
    {
        client = new TcpClient();

        try
        {
            // Kết nối tới Server
            Console.WriteLine(
                $"Dang ket noi toi {NetworkConstants.ServerIp}:{NetworkConstants.ServerPort}..."
            );

            await client.ConnectAsync(
                NetworkConstants.ServerIp,
                NetworkConstants.ServerPort
            );

            Console.WriteLine("Da ket noi Server!");
            Console.WriteLine("Nhao tin nhan de chat.");
            Console.WriteLine("Nhap /exit de thoat.");
            Console.WriteLine();

            // Chạy nhận tin nhắn và gửi tin nhắn đồng thời
            Task receiveTask = ReceiveMessages();
            Task sendTask = SendMessages();

            await Task.WhenAny(
                receiveTask,
                sendTask
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Khong the ket noi Server: {ex.Message}"
            );
        }
        finally
        {
            CloseClient();
        }
    }

    // Nhận tin nhắn từ Server
    private static async Task ReceiveMessages()
    {
        try
        {
            NetworkStream stream = client.GetStream();

            byte[] buffer = new byte[4096];

            while (true)
            {
                int bytesRead =
                    await stream.ReadAsync(
                        buffer,
                        0,
                        buffer.Length
                    );

                // Server đóng kết nối
                if (bytesRead == 0)
                {
                    Console.WriteLine(
                        "\nServer da dong ket noi."
                    );

                    break;
                }

                string message =
                    Encoding.UTF8.GetString(
                        buffer,
                        0,
                        bytesRead
                    );

                Console.WriteLine(
                    $"\n[Nhan] {message}"
                );

                Console.Write("> ");
            }
        }
        catch
        {
            Console.WriteLine(
                "\nMat ket noi voi Server."
            );
        }
    }

    // Gửi tin nhắn tới Server
    private static async Task SendMessages()
    {
        try
        {
            NetworkStream stream = client.GetStream();

            while (true)
            {
                Console.Write("> ");

                string? message = Console.ReadLine();

                if (string.IsNullOrEmpty(message))
                {
                    continue;
                }

                // Thoát
                if (message.ToLower() == "/exit")
                {
                    break;
                }

                byte[] data =
                    Encoding.UTF8.GetBytes(message);

                await stream.WriteAsync(
                    data,
                    0,
                    data.Length
                );
            }
        }
        catch
        {
            Console.WriteLine(
                "\nKhong the gui tin nhan."
            );
        }
    }

    // Đóng Client an toàn
    private static void CloseClient()
    {
        try
        {
            client?.GetStream().Close();
        }
        catch
        {
        }

        try
        {
            client?.Close();
        }
        catch
        {
        }

        try
        {
            client?.Dispose();
        }
        catch
        {
        }
    }
}