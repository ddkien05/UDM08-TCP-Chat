using System.Collections.Concurrent;
using System.Net.Sockets;
using ChatTCP.Common.Models;
using ChatTCP.Common.Protocol;

namespace ChatTCP.Server.Services;

public class MessageRouter(ConcurrentDictionary<string, NetworkStream> clientMap)
{
    /// <summary>
    /// Định tuyến tin nhắn chat đến người nhận dựa trên loại và ID của người nhận.
    /// </summary>
   public async Task RouteChatMessageAsync(Packet<ChatMessageData> chatPacket, NetworkStream senderStream)
    {
        try
        {
            var messageData = chatPacket.Data;
            if (messageData.TargetType == "PRIVATE")
            {
                if (clientMap.TryGetValue(messageData.TargetId, out var targetStream))
                {
                    Console.WriteLine($"[ROUTER] Message forwarded: {messageData.Sender.UserId} -> {messageData.TargetId}");
                    await MessageProtocol.SendPacketAsync(targetStream, chatPacket);
                }
                else
                {
                    Console.WriteLine($"[ROUTER] User {messageData.TargetId} is Offline");

                    var errorPacket = new Packet<ErrorData>
                    {
                        Type = "ERROR",
                        Seq = chatPacket.Seq,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Data = new ErrorData
                        {
                            Code = 404,
                            Message = $"User {messageData.TargetId} is Offline or does not exist."
                        }
                    };

                    await MessageProtocol.SendPacketAsync(senderStream, errorPacket);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Error routing message: {ex.Message}");
        }
    }
}