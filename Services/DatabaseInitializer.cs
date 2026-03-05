using MySql.Data.MySqlClient;

namespace MyWPFCRUDApp.Services
{
    public static class DatabaseInitializer
    {
        public static void InitializeAllTables()
        {
            using var conn = new MySqlConnection(DatabaseHelper.ConnectionString);
            conn.Open();

            // All CREATE TABLE statements in one place
            // Order matters — parent tables before child tables
            string[] tables = {
                @"CREATE TABLE IF NOT EXISTS MCategory (
                    Id           BIGINT AUTO_INCREMENT PRIMARY KEY,
                    CategoryName VARCHAR(100) NOT NULL UNIQUE,
                    CreatedBy    VARCHAR(100) DEFAULT 'System',
                    CreatedDate  DATETIME     DEFAULT CURRENT_TIMESTAMP,
                    ModifiedBy   VARCHAR(100) DEFAULT 'System',
                    ModifiedDate DATETIME     DEFAULT CURRENT_TIMESTAMP
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",

                

                // Add all your other tables here...
            };

            foreach (var sql in tables)
                new MySqlCommand(sql, conn).ExecuteNonQuery();
        }
    }
}