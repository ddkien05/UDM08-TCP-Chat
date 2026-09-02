using Microsoft.Data.Sqlite;

namespace ChatTCP.Server.Data
{
    /// Tạo connection SQLite dùng chung cho các Repository.
    public static class DbConnectionFactory
    {
        private const string ConnectionString = "Data Source=ChatApp.db";

        /// Mở 1 connection mới tới file ChatApp.db gọi trong khối using để tự đóng sau khi dùng xong
        public static SqliteConnection Create()
        {
            var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            return conn;
        }
    }
}