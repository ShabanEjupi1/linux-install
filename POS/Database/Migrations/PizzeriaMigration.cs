using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using KosovaPOS.Database;

namespace KosovaPOS.Database.Migrations
{
    /// <summary>
    /// Migration to initialize pizzeria-specific data and tables
    /// </summary>
    public class PizzeriaMigration
    {
        public static async Task<bool> RunAsync()
        {
            try
            {
                var server = Environment.GetEnvironmentVariable("SQL_SERVER") ?? "DESKTOP-25RVD2U";
                var database = Environment.GetEnvironmentVariable("SQL_DATABASE") ?? "BMDData";
                var connectionString = $"Server={server};Database={database};Trusted_Connection=True;TrustServerCertificate=True;";
                
                // Read SQL script
                var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "Migrations", "InitializePizzeriaData.sql");
                if (!File.Exists(scriptPath))
                {
                    Console.WriteLine($"Migration script not found: {scriptPath}");
                    return false;
                }
                
                var script = await File.ReadAllTextAsync(scriptPath);
                
                // Execute script
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                
                // Split by GO statements and execute each batch
                var batches = script.Split(new[] { "\nGO", "\r\nGO" }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var batch in batches)
                {
                    if (string.IsNullOrWhiteSpace(batch)) continue;
                    
                    using var command = new SqlCommand(batch, connection);
                    command.CommandTimeout = 300; // 5 minutes
                    await command.ExecuteNonQueryAsync();
                }
                
                Console.WriteLine("? Pizzeria migration completed successfully!");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error running pizzeria migration: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }
    }
}
