using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace ChatTCP.Server.Data
{
    public static class DbConnectionFactory
    {
        private static readonly string DbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ChatApp.db");
        private static readonly string ConnectionString = $"Data Source={DbPath}";

        public static SqliteConnection Create()
        {
            var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            return conn;
        }

        public static void Initialize()
        {
            using var conn = Create();

            string schemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Schema.sql");
            if (File.Exists(schemaPath))
            {
                string sql = File.ReadAllText(schemaPath);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }
    }
}