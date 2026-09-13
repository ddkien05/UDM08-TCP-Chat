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

        Console.Write("Password: ");
        string password = Console.ReadLine()?.Trim() ?? "123456";

        Console.Write("Name: ");
        string name = Console.ReadLine()?.Trim() ?? "User";

        Console.Title = $"CLIENT: {name} ({userId})";

        var tcpClient = new TcpClient();
        try
        {
            await tcpClient.ConnectAsync("127.0.0.1", 8888);
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
        var authReq = new Packet<AuthRequestData>
        {
            Type = "AUTH_REQ",
            Seq = 1,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Data = new AuthRequestData
            {
                Username = userId,
                Password = password,
                DisplayName = name,
                AvatarUrl = null
            }
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
            // Cho phép nhập 1 người hoặc nhiều người (vd: 2, 3, 4)
            Console.Write("\nTo UserID(s): ");
            string? toInput = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(toInput)) continue;

            Console.Write("Content: ");
            string? txt = Console.ReadLine();
            if (string.IsNullOrEmpty(txt)) continue;

            // Tách chuỗi thành danh sách các UserID
            var targetList = toInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            var chatPacket = new Packet<ChatMessageData>
            {
                Type = "CHAT_MSG",
                Seq = 100,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new ChatMessageData
                {
                    MsgId = Guid.NewGuid().ToString("N"),
                    Sender = new SenderInfo { UserId = userId, DisplayName = name },
                    Content = txt,

                    // 1 người --> Dùng TargetId bình thường
                    // Từ 2 người trở lên --> Gán vào danh sách TargetIds để Forward
                    TargetType = targetList.Count > 1 ? "FORWARD_MULTIPLE" : "PRIVATE",
                    TargetId = targetList.Count == 1 ? targetList[0] : string.Empty,
                    TargetIds = targetList.Count > 1 ? targetList : null,
                    IsForwarded = targetList.Count > 1
                }
            };

            await MessageProtocol.SendPacketAsync(networkStream, chatPacket);
            Console.WriteLine("-> Sent!");
        }
    }
}