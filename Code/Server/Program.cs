using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Common;

namespace Server;

class Program
{
    private static readonly ConcurrentDictionary<string, NetworkStream> Clients = new();

    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        int port = 9000;

        var r = new MessageRouter(Clients);
        var s = new TcpListener(IPAddress.Any, port);
        s.Start();

        Console.Title = "TCP SERVER";
        Console.WriteLine($"[SERVER] Port {port}...");

        while (true)
        {
            var c = await s.AcceptTcpClientAsync();
            _ = Task.Run(() => HandleClientAsync(c, r));
        }
    }

    private static async Task HandleClientAsync(TcpClient c, MessageRouter r)
    {
        var ns = c.GetStream();
        string? uid = null;

        try
        {
            while (true)
            {
                string? raw = await MessageProtocol.ReceiveRawJsonAsync(ns);
                if (raw == null) break;

                var basePkt = JsonSerializer.Deserialize<Packet<JsonElement>>(raw);
                if (basePkt == null) continue;

                // 1. Đăng nhập
                if (basePkt.Type == "AUTH_REQ")
                {
                    uid = basePkt.Data.GetProperty("username").GetString() ?? "unknown";
                    Clients[uid] = ns;
                    Console.WriteLine($"[+] User '{uid}' Online");

                    var res = new Packet<object>
                    {
                        Type = "AUTH_RES",
                        Seq = basePkt.Seq,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Data = new { code = 200, message = "Success", user_id = uid }
                    };
                    await MessageProtocol.SendPacketAsync(ns, res);
                }
                // 2. Chat 1-1
                else if (basePkt.Type == "CHAT_MSG")
                {
                    var chatPkt = JsonSerializer.Deserialize<Packet<ChatMessageData>>(raw);
                    if (chatPkt != null) await r.RouteChatMessageAsync(chatPkt, ns);
                }
            }
        }
        catch { }
        finally
        {
            if (!string.IsNullOrEmpty(uid))
            {
                Clients.TryRemove(uid, out _);
                Console.WriteLine($"[-] User '{uid}' Offline");
            }
            ns.Close();
            c.Close();
        }
    }
}