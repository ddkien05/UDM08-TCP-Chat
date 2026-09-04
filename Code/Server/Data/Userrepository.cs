using System;
using Microsoft.Data.Sqlite;

namespace ChatTCP.Server.Data
{
    /// Triển khai IUserRepository bằng SQLite.
    public class UserRepository : IUserRepository
    {
        ///Tìm user theo Username. Trả về null nếu không tồn tại hoặc có lỗi truy vấn
        public UserModel GetByUsername(string username)
        {
            try
            {
                using var conn = DbConnectionFactory.Create();
                using var cmd = new SqliteCommand(
                    @"SELECT UserId, Username, PasswordHash, DisplayName, AvatarUrl
                      FROM Users WHERE Username = @username", conn);
                cmd.Parameters.AddWithValue("@username", username);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read()) return null;

                return MapReaderToUser(reader);
            }
            catch (SqliteException ex)
            {
                Console.WriteLine("[UserRepository] Lỗi GetByUsername: " + ex.Message);
                return null; // không để lỗi Database làm sập server, chỉ báo không tìm thấy
            }
        }

        /// Tìm user theo UserId. Trả về null nếu không tồn tại hoặc có lỗi truy vấn
        public UserModel GetById(int userId)
        {
            try
            {
                using var conn = DbConnectionFactory.Create();
                using var cmd = new SqliteCommand(
                    @"SELECT UserId, Username, PasswordHash, DisplayName, AvatarUrl
                      FROM Users WHERE UserId = @id", conn);
                cmd.Parameters.AddWithValue("@id", userId);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read()) return null;

                return MapReaderToUser(reader);
            }
            catch (SqliteException ex)
            {
                Console.WriteLine("[UserRepository] Lỗi GetById: " + ex.Message);
                return null;
            }
        }

        /// Tạo user mới, trả về UserId vừa tạo ném lại lỗi cho tầng gọi xử lý nếu thất bại
        public int CreateUser(string username, string passwordHash, string displayName)
        {
            try
            {
                using var conn = DbConnectionFactory.Create();
                using var cmd = new SqliteCommand(
                    @"INSERT INTO Users (Username, PasswordHash, DisplayName)
                      VALUES (@username, @hash, @displayName);
                      SELECT last_insert_rowid();", conn);
                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.AddWithValue("@hash", passwordHash);
                cmd.Parameters.AddWithValue("@displayName", displayName);

                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (SqliteException ex)
            {
                Console.WriteLine("[UserRepository] Lỗi CreateUser: " + ex.Message);
                throw; // tầng gọi sẽ bắt lại để xử lý vd báo FAIL về client, không để sập server
            }
        }

        ///Cập nhật đường dẫn avatar của 1 user
        public void UpdateAvatar(int userId, string avatarUrl)
        {
            try
            {
                using var conn = DbConnectionFactory.Create();
                using var cmd = new SqliteCommand(
                    "UPDATE Users SET AvatarUrl = @avatarUrl WHERE UserId = @userId", conn);
                cmd.Parameters.AddWithValue("@avatarUrl", avatarUrl);
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.ExecuteNonQuery();
            }
            catch (SqliteException ex)
            {
                Console.WriteLine("[UserRepository] Lỗi UpdateAvatar: " + ex.Message);
            }
        }

        ///Đánh dấu 1 user đang online hay offline
        public void SetOnlineStatus(int userId, bool isOnline)
        {
            try
            {
                using var conn = DbConnectionFactory.Create();
                using var cmd = new SqliteCommand(
                    "UPDATE Users SET IsOnline = @online WHERE UserId = @userId", conn);
                cmd.Parameters.AddWithValue("@online", isOnline ? 1 : 0);
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.ExecuteNonQuery();
            }
            catch (SqliteException ex)
            {
                Console.WriteLine("[UserRepository] Lỗi SetOnlineStatus: " + ex.Message);
            }
        }

        ///Gom logic đọc 1 dòng SqliteDataReader thành UserModel, tránh lặp code giữa GetByUsername/GetById.
        private static UserModel MapReaderToUser(SqliteDataReader reader) => new UserModel
        {
            UserId = reader.GetInt32(0),
            Username = reader.GetString(1),
            PasswordHash = reader.GetString(2),
            DisplayName = reader.GetString(3),
            AvatarUrl = reader.IsDBNull(4) ? null : reader.GetString(4)
        };
    }
}