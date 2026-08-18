using System.Collections.Concurrent;
using System.Net.Sockets;
using Common;

namespace Server;

public class MessageRouter(ConcurrentDictionary<string, NetworkStream> map)
{
    public async Task RouteChatMessageAsync(Packet<ChatMessageData> pkt, NetworkStream ns)
    {
        var d = pkt.Data;

        if (d.TargetType == "PRIVATE")
        {
            if (map.TryGetValue(d.TargetId, out var ts))
            {
                Console.WriteLine($"[ROUTER] {d.Sender.UserId} -> {d.TargetId}");
                await MessageProtocol.SendPacketAsync(ts, pkt);
            }
            else
            {
                Console.WriteLine($"[ROUTER] {d.TargetId} Offline");
                var err = new Packet<ErrorData>
                {
                    Type = "ERROR",
                    Seq = pkt.Seq,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Data = new() { Code = 404, Message = $"User '{d.TargetId}' Offline." }
                };
                await MessageProtocol.SendPacketAsync(ns, err);
            }
        }
    }
}