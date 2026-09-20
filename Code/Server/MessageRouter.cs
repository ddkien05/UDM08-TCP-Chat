using System.Collections.Concurrent;
using System.Net.Sockets;
using ChatTCP.Common.Models;
using ChatTCP.Common.Protocol;
using ChatTCP.Server.Data;
using ChatTCP.Server.Networking;

namespace ChatTCP.Server.Services;

public class MessageRouter
{
    private readonly ConcurrentDictionary<string, ClientSession> _clientMap;
    private readonly IMessageRepository _messageRepository;

    public MessageRouter(
        ConcurrentDictionary<string, ClientSession> clientMap,
        IMessageRepository messageRepository)
    {
        _clientMap = clientMap;
        _messageRepository = messageRepository;
    }

    /// <summary>
    /// Định tuyến tin nhắn PRIVATE.
    /// Hỗ trợ tin nhắn thường, Reply và Forward.
    /// </summary>
    public async Task RouteChatMessageAsync(
        Packet<ChatMessageData> chatPacket,
        ClientSession senderSession)
    {
        try
        {
            var messageData = chatPacket.Data;

            // Chỉ xử lý tin nhắn PRIVATE
            if (messageData.TargetType != "PRIVATE")
                return;

            // =====================================================
            // 1. KIỂM TRA REPLY
            // =====================================================

            if (messageData.ReplyTo != null &&
                !string.IsNullOrWhiteSpace(messageData.ReplyTo.MsgId))
            {
                if (int.TryParse(
                    messageData.ReplyTo.MsgId,
                    out int replyMessageId))
                {
                    var originalMessage =
                        _messageRepository.GetById(replyMessageId);

                    if (originalMessage == null)
                    {
                        await SendErrorAsync(
                            senderSession,
                            chatPacket.Seq,
                            404,
                            $"Không tìm thấy tin nhắn gốc ID={replyMessageId}."
                        );

                        return;
                    }

                    Console.WriteLine(
                        $"[REPLY] User {messageData.Sender.UserId} " +
                        $"reply MessageId={replyMessageId}"
                    );
                }
            }

            // =====================================================
            // 2. KIỂM TRA FORWARD
            // =====================================================

            /*
             * ChatMessageData hiện tại của m chỉ có:
             *
             * IsForwarded
             * ForwardFromName
             *
             * Chưa có ForwardFromMessageId.
             *
             * Vì vậy ở đây chỉ ghi nhận tin nhắn Forward,
             * chưa thể tìm MessageId gốc.
             */

            if (messageData.IsForwarded)
            {
                Console.WriteLine(
                    $"[FORWARD] User {messageData.Sender.UserId} " +
                    $"forward message from {messageData.ForwardFromName}"
                );
            }

            // =====================================================
            // 3. LẤY DANH SÁCH NGƯỜI NHẬN
            // =====================================================

            string[] targets = messageData.TargetId
                .Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries
                )
                .Select(x => x.Trim())
                .Distinct()
                .ToArray();

            if (targets.Length == 0)
            {
                await SendErrorAsync(
                    senderSession,
                    chatPacket.Seq,
                    400,
                    "Không có người nhận."
                );

                return;
            }

            // =====================================================
            // 4. GỬI MESSAGE ĐẾN TỪNG NGƯỜI NHẬN
            // =====================================================

            foreach (string targetId in targets)
            {
                if (_clientMap.TryGetValue(
                    targetId,
                    out var targetSession))
                {
                    // QUAN TRỌNG: phải khóa (WriteLock) trước khi ghi, vì stream này
                    // có thể đang được CHÍNH luồng của targetSession ghi phản hồi khác
                    // (VD: USER_LIST) tại cùng thời điểm. Ghi chồng chéo không khóa
                    // sẽ làm hỏng khung tin phía nhận, gây rớt kết nối đột ngột.
                    await targetSession.WriteLock.WaitAsync();
                    try
                    {
                        Console.WriteLine(
                            $"[ROUTER] " +
                            $"{messageData.Sender.UserId} -> {targetId}"
                        );

                        await MessageProtocol.SendPacketAsync(
                            targetSession.TcpClient.GetStream(),
                            chatPacket
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[ROUTER] Không thể gửi đến {targetId}: " +
                            ex.Message
                        );
                    }
                    finally
                    {
                        targetSession.WriteLock.Release();
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
        ClientSession senderSession,
        int seq,
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

        await senderSession.WriteLock.WaitAsync();
        try
        {
            await MessageProtocol.SendPacketAsync(
                senderSession.TcpClient.GetStream(),
                errorPacket
            );
        }
        finally
        {
            senderSession.WriteLock.Release();
        }
    }
}
