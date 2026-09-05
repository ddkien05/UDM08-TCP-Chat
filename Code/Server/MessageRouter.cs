using System.Collections.Concurrent;
using System.Net.Sockets;
using ChatTCP.Common.Models;
using ChatTCP.Common.Protocol;

namespace ChatTCP.Server.Services;

using System.Collections.Concurrent;
using System.Net.Sockets;
using ChatTCP.Common.Models;
using ChatTCP.Common.Protocol;
using ChatTCP.Server.Data;

namespace ChatTCP.Server.Services;

public class MessageRouter
{
    private readonly ConcurrentDictionary<string, NetworkStream> _clientMap;
    private readonly IMessageRepository _messageRepository;

    public MessageRouter(
        ConcurrentDictionary<string, NetworkStream> clientMap,
        IMessageRepository messageRepository)
    {
        _clientMap = clientMap;
        _messageRepository = messageRepository;
    }

    /// Định tuyến tin nhắn PRIVATE.
    /// Hỗ trợ:
    /// - Tin nhắn bình thường
    /// - Reply
    /// - Forward
    /// </summary>
    public async Task RouteChatMessageAsync(
        Packet<ChatMessageData> chatPacket,
        NetworkStream senderStream)
    {
        try
        {
            var messageData = chatPacket.Data;

            if (messageData.TargetType != "PRIVATE")
                return;

            // =====================================================
            // 1. KIỂM TRA REPLY
            // =====================================================

            if (messageData.ReplyToMessageId.HasValue)
            {
                int replyId = messageData.ReplyToMessageId.Value;

                var originalMessage = _messageRepository.GetById(replyId);

                if (originalMessage == null)
                {
                    await SendErrorAsync(
                        senderStream,
                        chatPacket.Seq,
                        404,
                        $"Không tìm thấy tin nhắn gốc ID={replyId}."
                    );

                    return;
                }

                Console.WriteLine(
                    $"[REPLY] User {messageData.Sender.UserId} reply MessageId={replyId}"
                );
            }


            // =====================================================
            // 2. KIỂM TRA FORWARD
            // =====================================================

            if (messageData.ForwardedFromMessageId.HasValue)
            {
                int forwardId = messageData.ForwardedFromMessageId.Value;

                var originalMessage =
                    _messageRepository.GetById(forwardId);

                if (originalMessage == null)
                {
                    await SendErrorAsync(
                        senderStream,
                        chatPacket.Seq,
                        404,
                        $"Không tìm thấy tin nhắn gốc ID={forwardId} để forward."
                    );

                    return;
                }

                Console.WriteLine(
                    $"[FORWARD] User {messageData.Sender.UserId} forward MessageId={forwardId}"
                );
            }


            // =====================================================
            // 3. FORWARD NHIỀU ĐÍCH
            // =====================================================

            /*
             * TargetId có thể chứa nhiều user:
             *
             * "user01,user02,user03"
             *
             * Server sẽ gửi cùng một packet đến tất cả user.
             */

            string[] targets = messageData.TargetId
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Distinct()
                .ToArray();


            // =====================================================
            // 4. GỬI ĐẾN TỪNG ĐÍCH
            // =====================================================

            foreach (string targetId in targets)
            {
                if (_clientMap.TryGetValue(targetId, out var targetStream))
                {
                    try
                    {
                        Console.WriteLine(
                            $"[ROUTER] {messageData.Sender.UserId} -> {targetId}"
                        );

                        await MessageProtocol.SendPacketAsync(
                            targetStream,
                            chatPacket
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[ROUTER] Không thể gửi đến {targetId}: {ex.Message}"
                        );
                    }
                }
                else
                {
                    Console.WriteLine(
                        $"[ROUTER] User {targetId} Offline"
                    );
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[ERROR] Error routing message: {ex.Message}"
            );
        }
    }


    // =============================================================
    // GỬI ERROR
    // =============================================================

    private static async Task SendErrorAsync(
        NetworkStream stream,
        long seq,
        int code,
        string message)
    {
        var errorPacket = new Packet<ErrorData>
        {
            Type = "ERROR",
            Seq = seq,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),

            Data = new ErrorData
            {
                Code = code,
                Message = message
            }
        };

        await MessageProtocol.SendPacketAsync(
            stream,
            errorPacket
        );
    }
}

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
