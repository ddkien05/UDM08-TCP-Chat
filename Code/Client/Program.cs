using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Common;

namespace Client;

class Program
{
    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        Console.Write("UserID: ");
        string uid = Console.ReadLine()?.Trim() ?? "usr_101";

        Console.Write("Name: ");
        string name = Console.ReadLine()?.Trim() ?? "User";

        Console.Title = $"CLIENT: {name} ({uid})";

        var cli = new TcpClient();
        try
        {
            await cli.ConnectAsync("127.0.0.1", 9000);
            Console.WriteLine("Success!\n");
        }
        catch
        {
            Console.WriteLine("Cannot connect to Server!");
            Console.ReadLine();
            return;
        }

        var ns = cli.GetStream();

        // 1. Gui AUTH_REQ
        var req = new Packet<object>
        {
            Type = "AUTH_REQ",
            Seq = 1,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Data = new { username = uid, display_name = name }
        };
        await MessageProtocol.SendPacketAsync(ns, req);

        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    string? raw = await MessageProtocol.ReceiveRawJsonAsync(ns);
                    if (raw == null) break;

                    var basePkt = JsonSerializer.Deserialize<Packet<JsonElement>>(raw);
                    if (basePkt?.Type == "CHAT_MSG")
                    {
                        var chatPkt = JsonSerializer.Deserialize<Packet<ChatMessageData>>(raw);
                        var s = chatPkt?.Data.Sender;
                        Console.WriteLine($"\n📩 [{s?.DisplayName}]: {chatPkt?.Data.Content}");
                    }
                    else if (basePkt?.Type == "ERROR")
                    {
                        var err = JsonSerializer.Deserialize<Packet<ErrorData>>(raw);
                        Console.WriteLine($"[ERROR]: {err?.Data.Message}");
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

            var pkt = new Packet<ChatMessageData>
            {
                Type = "CHAT_MSG",
                Seq = 100,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new()
                {
                    MsgId = Guid.NewGuid().ToString("N"),
                    TargetType = "PRIVATE",
                    TargetId = to,
                    Sender = new() { UserId = uid, DisplayName = name },
                    Content = txt
                }
            };

            await MessageProtocol.SendPacketAsync(ns, pkt);
            Console.WriteLine("-> Sent!");
        }
    }
}