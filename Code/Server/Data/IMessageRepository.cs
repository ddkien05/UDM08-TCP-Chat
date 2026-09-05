using System.Collections.Generic;

namespace ChatTCP.Server.Data
{
    public interface IMessageRepository
    {
        // Lưu tin nhắn mới
        int InsertMessage(
            int conversationId,
            int senderId,
            string content,
            int? replyToMessageId = null,
            int? forwardedFromMessageId = null
        );

        // Tìm tin nhắn theo MessageId
        MessageModel GetById(int messageId);

        // Forward tin nhắn sang cuộc hội thoại khác
        int ForwardMessage(
            int sourceMessageId,
            int targetConversationId,
            int forwardedByUserId
        );

        // Lấy các tin nhắn gần nhất trong cuộc hội thoại
        List<MessageModel> GetRecentByConversation(
            int conversationId,
            int limit = 50
        );
    }
}
