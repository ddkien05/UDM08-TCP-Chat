using System;
using System.Collections.Generic;

namespace ChatTCP.Server.Data
{
    ///Đại diện cho 1 dòng dữ liệu trong bảng Messages
    public class MessageModel
    {
        public int MessageId { get; set; }
        public int ConversationId { get; set; }
        public int SenderId { get; set; }
        public string Content { get; set; }
        public int? ReplyToMessageId { get; set; }
        public int? ForwardedFromMessageId { get; set; }
        public DateTime SentAt { get; set; }
    }

    ///Hợp đồng thao tác với bảng Messages
    public interface IMessageRepository
    {
        ///Lưu 1 tin nhắn mới, trả về MessageId vừa tạo
        int InsertMessage(int conversationId, int senderId, string content,
                           int? replyToMessageId = null, int? forwardedFromMessageId = null);

        ///Forward tin nhắn gốc sang 1 cuộc hội thoại khác, trả về MessageId mới
        int ForwardMessage(int sourceMessageId, int targetConversationId, int forwardedByUserId);

        ///Tìm 1 tin nhắn theo MessageId. Trả về null nếu không tồn tại.
        MessageModel GetById(int messageId);

        ///Lấy các tin nhắn gần nhất của 1 cuộc hội thoại, sắp xếp cũ thành mới.
        List<MessageModel> GetRecentByConversation(int conversationId, int limit = 50);
    }
}