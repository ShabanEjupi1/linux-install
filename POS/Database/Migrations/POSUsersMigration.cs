using System;
using Microsoft.Data.SqlClient;

namespace KosovaPOS.Database.Migrations
{
    /// <summary>
    /// Utility class to ensure POSUsers table has all required columns
    /// This will automatically add missing columns when the application starts
    /// </summary>
    public static class POSUsersMigration
    {

        /// <summary>
        /// Synchronous version - runs migration without async to avoid deadlocks
        /// </summary>
        public static bool EnsureSchema()
        {
            try
            {
                var server = Environment.GetEnvironmentVariable("SQL_SERVER") ?? "DESKTOP-25RVD2U";
                var database = Environment.GetEnvironmentVariable("SQL_DATABASE") ?? "BMDData";
                var connectionString = $"Server={server};Database={database};Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;";

                using var connection = new SqlConnection(connectionString);
                connection.Open();

                // Check if table exists
                var tableExists = CheckTableExists(connection);
                
                if (!tableExists)
                {
                    CreateTable(connection);
                    return true;
                }

                // Add missing columns
                AddMissingColumns(connection);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error ensuring POSUsers schema: {ex.Message}");
                return false;
            }
        }

        private static bool CheckTableExists(SqlConnection connection)
        {
            var sql = @"SELECT COUNT(*) FROM sys.tables WHERE name = 'POSUsers'";
            using var command = new SqlCommand(sql, connection);
            var count = (int)command.ExecuteScalar();
            return count > 0;
        }

        private static void CreateTable(SqlConnection connection)
        {
            var sql = @"
                CREATE TABLE POSUsers (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Username NVARCHAR(100) NOT NULL,
                    PasswordHash NVARCHAR(256) NOT NULL,
                    Email NVARCHAR(100) NULL,
                    FullName NVARCHAR(200) NOT NULL,
                    Role NVARCHAR(50) NOT NULL DEFAULT 'Cashier',
                    IsActive BIT NOT NULL DEFAULT 1,
                    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                    LastLogin DATETIME NULL,
                    Branch NVARCHAR(200) NULL,
                    PhoneNumber NVARCHAR(50) NULL,
                    CanManageArticles BIT NOT NULL DEFAULT 1,
                    CanManagePurchases BIT NOT NULL DEFAULT 0,
                    CanManageUsers BIT NOT NULL DEFAULT 0,
                    CanViewReports BIT NOT NULL DEFAULT 1,
                    CanModifyPrices BIT NOT NULL DEFAULT 0,
                    CanDeleteReceipts BIT NOT NULL DEFAULT 0,
                    CanGiveDiscounts BIT NOT NULL DEFAULT 0,
                    MaxDiscountPercent DECIMAL(5,2) NOT NULL DEFAULT 0
                );

                -- Create default admin user (password: admin123)
                INSERT INTO POSUsers (Username, PasswordHash, Email, FullName, Role, IsActive, CreatedAt,
                                      CanManageArticles, CanManagePurchases, CanManageUsers, CanViewReports,
                                      CanModifyPrices, CanDeleteReceipts, CanGiveDiscounts, MaxDiscountPercent)
                VALUES ('admin', 'admin123', 'admin@example.com', 'Administrator', 'Admin', 1, GETDATE(),
                        1, 1, 1, 1, 1, 1, 1, 100);
            ";

            using var command = new SqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        private static void AddMissingColumns(SqlConnection connection)
        {
            var columns = new[]
            {
                ("Branch", "NVARCHAR(200) NULL"),
                ("Email", "NVARCHAR(100) NULL"),
                ("PhoneNumber", "NVARCHAR(50) NULL"),
                ("LastLogin", "DATETIME NULL"),
                ("PasswordHash", "NVARCHAR(256) NOT NULL DEFAULT ''"),
                ("CanManageArticles", "BIT NOT NULL DEFAULT 1"),
                ("CanManagePurchases", "BIT NOT NULL DEFAULT 0"),
                ("CanManageUsers", "BIT NOT NULL DEFAULT 0"),
                ("CanViewReports", "BIT NOT NULL DEFAULT 1"),
                ("CanModifyPrices", "BIT NOT NULL DEFAULT 0"),
                ("CanDeleteReceipts", "BIT NOT NULL DEFAULT 0"),
                ("CanGiveDiscounts", "BIT NOT NULL DEFAULT 0"),
                ("MaxDiscountPercent", "DECIMAL(5,2) NOT NULL DEFAULT 0")
            };

            foreach (var (columnName, columnType) in columns)
            {
                if (!ColumnExists(connection, columnName))
                {
                    AddColumn(connection, columnName, columnType);
                }
            }
        }

        private static bool ColumnExists(SqlConnection connection, string columnName)
        {
            var sql = @"
                SELECT COUNT(*) 
                FROM sys.columns 
                WHERE object_id = OBJECT_ID('POSUsers') AND name = @ColumnName";
            
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@ColumnName", columnName);
            var count = (int)command.ExecuteScalar();
            return count > 0;
        }

        private static void AddColumn(SqlConnection connection, string columnName, string columnType)
        {
            var sql = $"ALTER TABLE POSUsers ADD {columnName} {columnType}";
            using var command = new SqlCommand(sql, connection);
            command.ExecuteNonQuery();
            Console.WriteLine($"Added column: {columnName}");
        }
    }
}
