using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace ChatTCP.Server.Data
{
    public static class DbConnectionFactory
    {
        private static readonly string DbPath =
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "ChatApp.db");

        private static readonly string ConnectionString =
            $"Data Source={DbPath}";

        public static SqliteConnection Create()
        {
            var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            return conn;
        }

        public static void Initialize()
        {
            string schemaPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Data",
                "Schema.sql");

            // Kiểm tra Schema.sql có tồn tại không
            if (!File.Exists(schemaPath))
            {
                throw new FileNotFoundException(
                    "Khong tim thay file Schema.sql.",
                    schemaPath);
            }

            using var conn = Create();

            // Đọc cấu trúc database
            string sql = File.ReadAllText(schemaPath);

            // Thực thi Schema.sql
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();

            Console.WriteLine(
                "Database va cac Bang da duoc khoi tao thanh cong!");

            Console.WriteLine(
                $"Database: {DbPath}");
        }
    }
}