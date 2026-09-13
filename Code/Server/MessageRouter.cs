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

            //TH1: Forward tới nhiều người nhận
            if (messageData.TargetIds != null && messageData.TargetIds.Count > 0)
            {
                var offlineUsers = new List<string>();
                foreach (var targetId in messageData.TargetIds)
                {
                    // Không tự gửi lại cho chính người gửi
                    if (targetId == messageData.Sender.UserId) continue;

                    if (clientMap.TryGetValue(targetId, out var targetStream))
                    {
                        Console.WriteLine($"[ROUTER - FORWARD] {messageData.Sender.UserId} -> {targetId}");
                        await MessageProtocol.SendPacketAsync(targetStream, chatPacket);
                    }
                    else
                    {
                        offlineUsers.Add(targetId);
                    }
                }

                // Nếu có người dùng trong danh sách đang offline, báo lại cho sender 1 lần duy nhất
                if (offlineUsers.Count > 0)
                {
                    var errorPacket = new Packet<ErrorData>
                    {
                        Type = "ERROR",
                        Seq = chatPacket.Seq,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Data = new ErrorData
                        {
                            Code = 404,
                            Message = $"Các người dùng sau đang Offline hoặc không tồn tại: {string.Join(", ", offlineUsers)}"
                        }
                    };
                    await MessageProtocol.SendPacketAsync(senderStream, errorPacket);
                }
            }

            //TH2: Forward tới 1 người nhận
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