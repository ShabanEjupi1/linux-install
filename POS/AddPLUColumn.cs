using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace KosovaPOS
{
    /// <summary>
    /// Adds PLU column to Articles table
    /// </summary>
    class AddPLUColumn
    {
        static void Main(string[] args)
        {
            var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "Database", "KosovaPOS.db");
            
            if (!File.Exists(dbPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Database not found at: {dbPath}");
                Console.ResetColor();
                return;
            }
            
            try
            {
                var connectionString = $"Data Source={dbPath}";
                using var connection = new SqliteConnection(connectionString);
                connection.Open();
                
                // Check if PLU column already exists
                using (var checkCmd = connection.CreateCommand())
                {
                    checkCmd.CommandText = "PRAGMA table_info(Articles);";
                    using var reader = checkCmd.ExecuteReader();
                    bool pluExists = false;
                    
                    while (reader.Read())
                    {
                        var columnName = reader.GetString(1);
                        if (columnName.Equals("PLU", StringComparison.OrdinalIgnoreCase))
                        {
                            pluExists = true;
                            break;
                        }
                    }
                    
                    if (pluExists)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("PLU column already exists in Articles table.");
                        Console.ResetColor();
                        return;
                    }
                }
                
                // Add PLU column
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "ALTER TABLE Articles ADD COLUMN PLU INTEGER NULL;";
                    cmd.ExecuteNonQuery();
                }
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ PLU column successfully added to Articles table!");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Error: {ex.Message}");
                Console.ResetColor();
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
