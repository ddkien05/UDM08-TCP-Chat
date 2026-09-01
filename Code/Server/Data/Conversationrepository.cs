using ChatTCP.Sever.Data;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace ChatTCP.Server.Data
{
   
    public class ConversationRepository : IConversationRepository
    {
        
        public int CreateConversation(bool isGroup, string name, IEnumerable<int> memberUserIds)
        {
            using var conn = DbConnectionFactory.Create();
            using var tx = conn.BeginTransaction();

            try
            {
                int conversationId;
                using (var cmd = new SqliteCommand(
                    @"INSERT INTO Conversations (IsGroup, Name)
                      VALUES (@isGroup, @name);
                      SELECT last_insert_rowid();", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@isGroup", isGroup ? 1 : 0);
                    cmd.Parameters.AddWithValue("@name", (object)name ?? DBNull.Value);
                    conversationId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                foreach (var userId in memberUserIds)
                {
                    using var cmd = new SqliteCommand(
                        @"INSERT INTO ConversationMembers (ConversationId, UserId)
                          VALUES (@convId, @userId)", conn, tx);
                    cmd.Parameters.AddWithValue("@convId", conversationId);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                return conversationId;
            }
            catch (SqliteException ex)
            {
                Console.WriteLine("[ConversationRepository] Lỗi CreateConversation, đang rollback: " + ex.Message);
                tx.Rollback(); 
                throw;
            }
        }

        /// Thêm 1 thành viên vào hội thoại đã có sẵn.
        public void AddMember(int conversationId, int userId)
        {
            try
            {
                using var conn = DbConnectionFactory.Create();
                using var cmd = new SqliteCommand(
                    @"INSERT INTO ConversationMembers (ConversationId, UserId)
                      VALUES (@convId, @userId)", conn);
                cmd.Parameters.AddWithValue("@convId", conversationId);
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.ExecuteNonQuery();
            }
            catch (SqliteException ex)
            {
                Console.WriteLine("[ConversationRepository] Lỗi AddMember: " + ex.Message);
                throw;
            }
        }

        /// Lấy danh sách UserId là thành viên của 1 cuộc hội thoại. Trả về danh sách rỗng nếu có lỗi.
        public List<int> GetMemberUserIds(int conversationId)
        {
            var result = new List<int>();
            try
            {
                using var conn = DbConnectionFactory.Create();
                using var cmd = new SqliteCommand(
                    "SELECT UserId FROM ConversationMembers WHERE ConversationId = @convId", conn);
                cmd.Parameters.AddWithValue("@convId", conversationId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    result.Add(reader.GetInt32(0));
            }
            catch (SqliteException ex)
            {
                Console.WriteLine("[ConversationRepository] Lỗi GetMemberUserIds: " + ex.Message);
            }
            return result;
        }

        ///Lấy danh sách hội thoại mà 1 user đang tham gia. Trả về danh sách rỗng nếu có lỗi.
        public List<ConversationModel> GetConversationsByUser(int userId)
        {
            var result = new List<ConversationModel>();
            try
            {
                using var conn = DbConnectionFactory.Create();
                using var cmd = new SqliteCommand(
                    @"SELECT c.ConversationId, c.IsGroup, c.Name
                      FROM Conversations c
                      JOIN ConversationMembers cm ON cm.ConversationId = c.ConversationId
                      WHERE cm.UserId = @userId", conn);
                cmd.Parameters.AddWithValue("@userId", userId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(new ConversationModel
                    {
                        ConversationId = reader.GetInt32(0),
                        IsGroup = reader.GetInt32(1) == 1,
                        Name = reader.IsDBNull(2) ? null : reader.GetString(2)
                    });
                }
            }
            catch (SqliteException ex)
            {
                Console.WriteLine("[ConversationRepository] Lỗi GetConversationsByUser: " + ex.Message);
            }
            return result;
        }
    }
}