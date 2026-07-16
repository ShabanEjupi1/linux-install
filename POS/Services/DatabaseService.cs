using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using KosovaPOS.Database;

namespace KosovaPOS.Services
{
    public class DatabaseService
    {
        private readonly string _server;
        private readonly string _database;

        public DatabaseService()
        {
            _server = Environment.GetEnvironmentVariable("SQL_SERVER") ?? "(localdb)\\MSSQLLocalDB";
            _database = Environment.GetEnvironmentVariable("SQL_DATABASE") ?? "BMDData";
        }

        public static bool EnsureDatabaseInitialized()
        {
            try
            {
                var service = new DatabaseService();
                
                // Create database if not exists
                if (!service.DatabaseExists())
                {
                    if (!service.CreateDatabase())
                    {
                        return false;
                    }
                }

                // Apply migrations
                using (var context = new POSDbContext())
                {
                    var pendingMigrations = context.Database.GetPendingMigrations().ToList();
                    if (pendingMigrations.Any())
                    {
                        context.Database.Migrate();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_errors.log"),
                    $"\n[{DateTime.Now}] DATABASE INIT ERROR: {ex.Message}\n{ex.StackTrace}\n"
                );
                return false;
            }
        }

        public bool DatabaseExists()
        {
            try
            {
                var connectionString = $"Server={_server};Initial Catalog=master;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=5;";

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (var command = new SqlCommand(
                        $"SELECT database_id FROM sys.databases WHERE name = @dbName",
                        connection))
                    {
                        command.Parameters.AddWithValue("@dbName", _database);
                        var result = command.ExecuteScalar();
                        return result != null;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public bool CreateDatabase()
        {
            try
            {
                var connectionString = $"Server={_server};Initial Catalog=master;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=5;";

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (var command = new SqlCommand(
                        $@"IF NOT EXISTS(SELECT * FROM sys.databases WHERE name = @dbName)
                           BEGIN
                               CREATE DATABASE [{_database}]
                           END",
                        connection))
                    {
                        command.Parameters.AddWithValue("@dbName", _database);
                        command.ExecuteNonQuery();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_errors.log"),
                    $"\n[{DateTime.Now}] DATABASE CREATE ERROR: {ex.Message}\n"
                );
                return false;
            }
        }

        public static bool BackupDatabase(string backupPath)
        {
            try
            {
                var server = Environment.GetEnvironmentVariable("SQL_SERVER") ?? "(localdb)\\MSSQLLocalDB";
                var database = Environment.GetEnvironmentVariable("SQL_DATABASE") ?? "BMDData";
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFile = Path.Combine(backupPath, $"{database}_{timestamp}.bak");

                Directory.CreateDirectory(backupPath);

                var connectionString = $"Server={server};Initial Catalog={database};Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;";

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    var backupCommand = $@"
                        BACKUP DATABASE [{database}]
                        TO DISK = @backupFile
                        WITH FORMAT,
                             MEDIANAME = '{database}_Backup',
                             NAME = '{database} Full Backup',
                             DESCRIPTION = 'Full backup of {database}'
                    ";

                    using (var command = new SqlCommand(backupCommand, connection))
                    {
                        command.CommandTimeout = 300;
                        command.Parameters.AddWithValue("@backupFile", backupFile);
                        command.ExecuteNonQuery();
                    }
                }

                return File.Exists(backupFile);
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_errors.log"),
                    $"\n[{DateTime.Now}] DATABASE BACKUP ERROR: {ex.Message}\n"
                );
                return false;
            }
        }

        public static bool RestoreDatabase(string backupFile)
        {
            try
            {
                if (!File.Exists(backupFile))
                {
                    return false;
                }

                var server = Environment.GetEnvironmentVariable("SQL_SERVER") ?? "(localdb)\\MSSQLLocalDB";
                var database = Environment.GetEnvironmentVariable("SQL_DATABASE") ?? "BMDData";

                var connectionString = $"Server={server};Initial Catalog=master;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;";

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Kill existing connections
                    using (var killCommand = new SqlCommand(
                        $@"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                           ALTER DATABASE [{database}] SET MULTI_USER;",
                        connection))
                    {
                        killCommand.CommandTimeout = 30;
                        killCommand.ExecuteNonQuery();
                    }

                    // Restore database
                    var restoreCommand = $@"
                        RESTORE DATABASE [{database}]
                        FROM DISK = @backupFile
                        WITH REPLACE
                    ";

                    using (var command = new SqlCommand(restoreCommand, connection))
                    {
                        command.CommandTimeout = 300;
                        command.Parameters.AddWithValue("@backupFile", backupFile);
                        command.ExecuteNonQuery();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_errors.log"),
                    $"\n[{DateTime.Now}] DATABASE RESTORE ERROR: {ex.Message}\n"
                );
                return false;
            }
        }
    }
}
