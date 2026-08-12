using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Common;

class Program
{
    // Lưu danh sách client đang kết nối
    static ConcurrentDictionary<string, TcpClient> clients = new();

    // ID tăng dần cho mỗi tin nhắn
    static int messageId = 0;

    static async Task Main(string[] args)
    {
        TcpListener listener = new TcpListener(IPAddress.Any, 5000);

        listener.Start();

        Console.WriteLine("=================================");
        Console.WriteLine(" TCP CHAT SERVER");
        Console.WriteLine(" Server đang chạy port 5000...");
        Console.WriteLine("=================================");

        while (true)
        {
            TcpClient client = await listener.AcceptTcpClientAsync();

            _ = HandleClient(client);
        }
    }

    static async Task HandleClient(TcpClient client)
    {
        string? username = null;

        try
        {
            NetworkStream stream = client.GetStream();

            using StreamReader reader = new StreamReader(
                stream,
                Encoding.UTF8
            );

            using StreamWriter writer = new StreamWriter(
                stream,
                Encoding.UTF8
            )
            {
                AutoFlush = true
            };

            // Client gửi username đầu tiên
            username = await reader.ReadLineAsync();

            if (string.IsNullOrWhiteSpace(username))
            {
                client.Close();
                return;
            }

            clients[username] = client;

            Console.WriteLine($"[JOIN] {username} đã kết nối.");

            await writer.WriteLineAsync(
                $"SERVER: Xin chào {username}!"
            );

            while (true)
            {
                string? line = await reader.ReadLineAsync();

                if (line == null)
                    break;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                Message? message = Message.FromJson(line);

                if (message == null)
                    continue;

                message.MessageId = ++messageId;
                message.Sender = username;
                message.SentAt = DateTime.Now;

                Console.WriteLine(
                    $"[{message.MessageId}] " +
                    $"{message.Sender} -> {message.Receiver}: " +
                    $"{message.Content}"
                );

                // Kiểm tra người nhận có online không
                if (clients.TryGetValue(
                    message.Receiver,
                    out TcpClient? receiverClient))
                {
                    string json = message.ToJson();

                    byte[] data = Encoding.UTF8.GetBytes(
                        json + Environment.NewLine
                    );

                    await receiverClient
                        .GetStream()
                        .WriteAsync(data);
                }
                else
                {
                    await writer.WriteLineAsync(
                        "SERVER: Người nhận hiện không online."
                    );
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[ERROR] {ex.Message}"
            );
        }
        finally
        {
            if (username != null)
            {
                clients.TryRemove(
                    username,
                    out _
                );

                Console.WriteLine(
                    $"[LEAVE] {username} đã thoát."
                );
            }

            client.Close();
        }
    }
}
