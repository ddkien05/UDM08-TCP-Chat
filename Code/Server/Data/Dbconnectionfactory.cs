using Microsoft.Data.Sqlite;

namespace ChatTCP.Sever.Data
{
    public static class DbConnectionFactory
    {
        private const string ConnectionString = "Data Source=ChatApp.db";

        public static SqliteConnection Create()
        {
            var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            return conn;
        }
    }
}