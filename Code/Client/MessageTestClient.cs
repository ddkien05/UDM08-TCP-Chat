using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ChatTCP.Common.Models;
using ChatTCP.Common.Protocol;

namespace ChatTCP.Client.Networking;

class MessageTestClient
{
    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        Console.Write("UserID: ");
        string userId = Console.ReadLine()?.Trim() ?? "usr_101";

        Console.Write("Name: ");
        string name = Console.ReadLine()?.Trim() ?? "User";

        Console.Title = $"CLIENT: {name} ({userId})";

        var tcpClient = new TcpClient();
        try
        {
            await tcpClient.ConnectAsync("127.0.0.1", 9000);
            Console.WriteLine("Success!\n");
        }
        catch
        {
            Console.WriteLine("Cannot connect to Server!");
            Console.ReadLine();
            return;
        }

        var networkStream = tcpClient.GetStream();

        // Gui AUTH_REQ
        var authReq = new Packet<object>
        {
            Type = "AUTH_REQ",
            Seq = 1,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Data = new { username = userId, display_name = name }
        };
        await MessageProtocol.SendPacketAsync(networkStream, authReq);

        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    string? raw = await MessageProtocol.ReceiveRawJsonAsync(networkStream);
                    if (raw == null) break;

                    var basePacket = JsonSerializer.Deserialize<Packet<JsonElement>>(raw);
                    if (basePacket?.Type == "CHAT_MSG")
                    {
                        var chatPacket = JsonSerializer.Deserialize<Packet<ChatMessageData>>(raw);
                        var sender = chatPacket?.Data.Sender;
                        Console.WriteLine($"\n📩 [{sender?.DisplayName}]: {chatPacket?.Data.Content}");
                    }
                    else if (basePacket?.Type == "ERROR")
                    {
                        var errorPacket = JsonSerializer.Deserialize<Packet<ErrorData>>(raw);
                        Console.WriteLine($"\n⚠️: {errorPacket?.Data.Message}");
                    }
                }
                catch { break; }
            }
        });

        await Task.Delay(300);
        while (true)
        {
            Console.Write("\nTo UserID: ");
            string? to = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(to)) continue;

            Console.Write("Content: ");
            string? txt = Console.ReadLine();
            if (string.IsNullOrEmpty(txt)) continue;

            var chatPacket = new Packet<ChatMessageData>
            {
                Type = "CHAT_MSG",
                Seq = 100,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new()
                {
                    MsgId = Guid.NewGuid().ToString("N"),
                    TargetType = "PRIVATE",
                    TargetId = to,
                    Sender = new() { UserId = userId, DisplayName = name },
                    Content = txt
                }
            };

            await MessageProtocol.SendPacketAsync(networkStream, chatPacket);
            Console.WriteLine("-> Sent!");
        }
    }
}