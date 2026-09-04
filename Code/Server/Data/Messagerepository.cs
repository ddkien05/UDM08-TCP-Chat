using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace ChatTCP.Server.Data
{
    ///Triển khai IMessageRepository bằng SQLite
    public class MessageRepository : IMessageRepository
    {
        ///Lưu 1 tin nhắn mới, trả về MessageId vừa tạo. Ném lại lỗi cho tầng gọi xử lý nếu thất bại.
        public int InsertMessage(int conversationId, int senderId, string content,
                                  int? replyToMessageId = null, int? forwardedFromMessageId = null)
        {
            try
            {
                using var conn = DbConnectionFactory.Create();
                using var cmd = new SqliteCommand(
                    @"INSERT INTO Messages
                        (ConversationId, SenderId, Content, ReplyToMessageId, ForwardedFromMessageId)
                      VALUES (@convId, @senderId, @content, @replyTo, @forwardedFrom);
                      SELECT last_insert_rowid();", conn);

                cmd.Parameters.AddWithValue("@convId", conversationId);
                cmd.Parameters.AddWithValue("@senderId", senderId);
                cmd.Parameters.AddWithValue("@content", content);
                cmd.Parameters.AddWithValue("@replyTo", (object)replyToMessageId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@forwardedFrom", (object)forwardedFromMessageId ?? DBNull.Value);

                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (SqliteException ex)
            {
                Console.WriteLine("[MessageRepository] Lỗi InsertMessage: " + ex.Message);
                throw;
            }
        }

        ///Forward tin nhắn gốc sang 1 cuộc hội thoại khác, trả về MessageId mới.
        public int ForwardMessage(int sourceMessageId, int targetConversationId, int forwardedByUserId)
        {
            MessageModel original = GetById(sourceMessageId);
            if (original == null)
                throw new InvalidOperationException($"Không tìm thấy MessageId={sourceMessageId} để forward");

            return InsertMessage(targetConversationId, forwardedByUserId, original.Content,
                                  replyToMessageId: null, forwardedFromMessageId: sourceMessageId);
        }

        ///Tìm 1 tin nhắn theo MessageId. Trả về null nếu không tồn tại hoặc có lỗi truy vấn.
        public MessageModel GetById(int messageId)
        {
            try
            {
                using var conn = DbConnectionFactory.Create();
                using var cmd = new SqliteCommand(
                    @"SELECT MessageId, ConversationId, SenderId, Content,
                             ReplyToMessageId, ForwardedFromMessageId, SentAt
                      FROM Messages WHERE MessageId = @id", conn);
                cmd.Parameters.AddWithValue("@id", messageId);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read()) return null;
                return MapReaderToMessage(reader);
            }
            catch (SqliteException ex)
            {
                Console.WriteLine("[MessageRepository] Lỗi GetById: " + ex.Message);
                return null;
            }
        }

        /// Lấy các tin nhắn gần nhất của 1 cuộc hội thoại, sắp xếp cũ -> mới
        public List<MessageModel> GetRecentByConversation(int conversationId, int limit = 50)
        {
            var result = new List<MessageModel>();

            try
            {
                using var conn = DbConnectionFactory.Create();
                using var cmd = new SqliteCommand(
                    @"SELECT MessageId, ConversationId, SenderId, Content,
                             ReplyToMessageId, ForwardedFromMessageId, SentAt
                      FROM Messages
                      WHERE ConversationId = @convId
                      ORDER BY SentAt DESC
                      LIMIT @limit", conn);
                cmd.Parameters.AddWithValue("@convId", conversationId);
                cmd.Parameters.AddWithValue("@limit", limit);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    result.Add(MapReaderToMessage(reader));

                result.Reverse(); // trả về theo thứ tự cũ -> mới cho dễ render lên GUI
            }
            catch (SqliteException ex)
            {
                Console.WriteLine("[MessageRepository] Lỗi GetRecentByConversation: " + ex.Message);
                // trả về danh sách rỗng thay vì để lỗi rơi ra ngoài làm sập server
            }

            return result;
        }

        private static MessageModel MapReaderToMessage(SqliteDataReader reader) => new MessageModel
        {
            MessageId = reader.GetInt32(0),
            ConversationId = reader.GetInt32(1),
            SenderId = reader.GetInt32(2),
            Content = reader.GetString(3),
            ReplyToMessageId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
            ForwardedFromMessageId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
            SentAt = reader.GetDateTime(6)
        };
    }
}