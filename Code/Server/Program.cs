using System.Net;
using System.Net.Sockets;
using System.Text;
using TcpChat.Common.Constants;

class Server
{
    // Server chạy ở port 9999
    private const int PORT = 9999;

    // TcpListener dùng để lắng nghe client
    private static TcpListener listener;

    // Danh sách client đang kết nối
    private static List<TcpClient> clients = new List<TcpClient>();

    // Lock để tránh nhiều client cùng sửa List
    private static readonly object clientsLock = new object();

    static async Task Main()
    {
        // IPAddress.Any:
        // Cho phép client kết nối thông qua mọi card mạng
        listener = new TcpListener(IPAddress.Any, NetworkConstants.ServerPort);

        listener.Start();

        Console.WriteLine("================================");
        Console.WriteLine("        TCP CHAT SERVER");
        Console.WriteLine("================================");
        Console.WriteLine($"Server dang chay tai port {PORT}");
        Console.WriteLine("Dang cho client...");
        Console.WriteLine();

        try
        {
            while (true)
            {
                // Chờ client kết nối
                TcpClient client = await listener.AcceptTcpClientAsync();

                // Hiển thị IP và port của client
                IPEndPoint endPoint =
                    (IPEndPoint)client.Client.RemoteEndPoint;

                Console.WriteLine(
                    $"[+] Client ket noi: {endPoint.Address}:{endPoint.Port}"
                );

                // Thêm client vào danh sách
                lock (clientsLock)
                {
                    clients.Add(client);

                    Console.WriteLine(
                        $"So client hien tai: {clients.Count}"
                    );
                }

                // Xử lý client riêng
                _ = HandleClient(client);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Server Error: {ex.Message}");
        }
        finally
        {
            StopServer();
        }
    }

    // Xử lý một client
    private static async Task HandleClient(TcpClient client)
    {
        try
        {
            NetworkStream stream = client.GetStream();

            byte[] buffer = new byte[4096];

            while (true)
            {
                // Nhận dữ liệu
                int bytesRead =
                    await stream.ReadAsync(buffer, 0, buffer.Length);

                // Client đã đóng kết nối
                if (bytesRead == 0)
                {
                    break;
                }

                string message =
                    Encoding.UTF8.GetString(buffer, 0, bytesRead);

                IPEndPoint endPoint =
                    (IPEndPoint)client.Client.RemoteEndPoint;

                Console.WriteLine(
                    $"[{endPoint.Address}:{endPoint.Port}] {message}"
                );

                // Gửi tin nhắn cho các client khác
                Broadcast(message, client);
            }
        }
        catch
        {
            // Client mất kết nối
        }
        finally
        {
            RemoveClient(client);
            CloseClient(client);
        }
    }

    // Xóa client khỏi danh sách
    private static void RemoveClient(TcpClient client)
    {
        lock (clientsLock)
        {
            if (clients.Contains(client))
            {
                clients.Remove(client);

                Console.WriteLine(
                    $"[-] Client da roi. Con {clients.Count} client."
                );
            }
        }
    }

    // Gửi tin nhắn cho tất cả client khác
    private static void Broadcast(
        string message,
        TcpClient sender)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);

        lock (clientsLock)
        {
            foreach (TcpClient client in clients.ToList())
            {
                // Không gửi lại cho người gửi
                if (client == sender)
                {
                    continue;
                }

                try
                {
                    if (client.Connected)
                    {
                        NetworkStream stream =
                            client.GetStream();

                        stream.Write(
                            data,
                            0,
                            data.Length
                        );
                    }
                }
                catch
                {
                    // Client lỗi sẽ được xử lý
                    // bởi HandleClient()
                }
            }
        }
    }

    // Đóng client an toàn
    private static void CloseClient(TcpClient client)
    {
        try
        {
            client.GetStream().Close();
        }
        catch
        {
        }

        try
        {
            client.Close();
        }
        catch
        {
        }

        try
        {
            client.Dispose();
        }
        catch
        {
        }
    }

    // Đóng toàn bộ server
    private static void StopServer()
    {
        Console.WriteLine("Dang dongServer...");

        lock (clientsLock)
        {
            foreach (TcpClient client in clients)
            {
                CloseClient(client);
            }

            clients.Clear();
        }

        try
        {
            listener.Stop();
        }
        catch
        {
        }

        Console.WriteLine("Server da dong.");
    }
}