using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

/// <summary>
/// Database cleanup tool to remove invalid articles and prepare for production.
/// Run this tool before deployment to clean up the database.
/// </summary>
class CleanupDatabase
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== KosovaPOS Database Cleanup Tool ===");
        Console.WriteLine();
        
        // Find database paths to clean
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var possiblePaths = new[]
        {
            Path.Combine(basePath, "Database", "KosovaPOS.db"),
            Path.Combine(basePath, "..", "..", "..", "..", "Database", "KosovaPOS.db"),
            Path.Combine(basePath, "..", "..", "..", "..", "publish", "production", "Database", "KosovaPOS.db"),
            @"C:\Users\Dell\Desktop\POS\Database\KosovaPOS.db",
            @"C:\Users\Dell\Desktop\POS\publish\production\Database\KosovaPOS.db",
            @"C:\Users\Dell\Desktop\POS\bin\Debug\net8.0-windows\win-x64\Database\KosovaPOS.db",
            @"C:\Users\Dell\Desktop\POS\bin\Release\net8.0-windows\win-x64\Database\KosovaPOS.db"
        };
        
        foreach (var dbPath in possiblePaths)
        {
            if (File.Exists(dbPath))
            {
                Console.WriteLine($"Found database: {dbPath}");
                CleanDatabase(dbPath);
                Console.WriteLine();
            }
        }
        
        Console.WriteLine("Cleanup complete!");
    }
    
    static void CleanDatabase(string dbPath)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            
            // Count articles to be deleted
            using (var countCmd = connection.CreateCommand())
            {
                countCmd.CommandText = @"
                    SELECT COUNT(*) FROM Articles 
                    WHERE Name = '--' 
                       OR Name = '-' 
                       OR Name = '' 
                       OR Name IS NULL
                       OR Name LIKE '--%'
                       OR TRIM(Name) = ''";
                var count = Convert.ToInt32(countCmd.ExecuteScalar());
                Console.WriteLine($"  Found {count} invalid articles (name is '--', empty, or null)");
                
                if (count > 0)
                {
                    // Delete invalid articles
                    using var deleteCmd = connection.CreateCommand();
                    deleteCmd.CommandText = @"
                        DELETE FROM Articles 
                        WHERE Name = '--' 
                           OR Name = '-' 
                           OR Name = '' 
                           OR Name IS NULL
                           OR Name LIKE '--%'
                           OR TRIM(Name) = ''";
                    var deleted = deleteCmd.ExecuteNonQuery();
                    Console.WriteLine($"  Deleted {deleted} invalid articles");
                }
            }
            
            // Count total remaining articles
            using (var totalCmd = connection.CreateCommand())
            {
                totalCmd.CommandText = "SELECT COUNT(*) FROM Articles WHERE IsActive = 1";
                var total = Convert.ToInt32(totalCmd.ExecuteScalar());
                Console.WriteLine($"  Total active articles remaining: {total}");
            }
            
            // Vacuum to reclaim space
            using (var vacuumCmd = connection.CreateCommand())
            {
                vacuumCmd.CommandText = "VACUUM";
                vacuumCmd.ExecuteNonQuery();
                Console.WriteLine("  Database vacuumed (space reclaimed)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error: {ex.Message}");
        }
    }
}
