// Quick tool to export products from POS database to JSON for web store
// Usage: dotnet run

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

class Program
{
    static void Main(string[] args)
    {
        string dbPath = args.Length > 0 ? args[0] : FindDatabase();
        string outputPath = args.Length > 1 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "WebStore", "public", "data", "products.json");

        if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
        {
            Console.WriteLine("❌ Database not found. Please provide the path as an argument.");
            Console.WriteLine("Usage: dotnet run <database_path> [output_path]");
            return;
        }

        Console.WriteLine($"📁 Database: {dbPath}");
        Console.WriteLine($"📤 Output: {outputPath}");

        try
        {
            var products = new List<object>();
            
            using (var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly"))
            {
                connection.Open();
                
                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT 
                        Id, Barcode, Name, Unit, SalesPrice, Category, Brand, 
                        Supplier, StockQuantity, PhotoPath, Size, Color
                    FROM Articles 
                    WHERE IsActive = 1
                    ORDER BY Name";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        products.Add(new
                        {
                            id = reader.GetInt32(0),
                            barcode = reader.IsDBNull(1) ? null : reader.GetString(1),
                            name = reader.IsDBNull(2) ? null : reader.GetString(2),
                            unit = reader.IsDBNull(3) ? null : reader.GetString(3),
                            salesPrice = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4),
                            category = reader.IsDBNull(5) ? null : reader.GetString(5),
                            brand = reader.IsDBNull(6) ? null : reader.GetString(6),
                            supplier = reader.IsDBNull(7) ? null : reader.GetString(7),
                            stockQuantity = reader.IsDBNull(8) ? 0m : reader.GetDecimal(8),
                            photoPath = reader.IsDBNull(9) ? null : reader.GetString(9),
                            size = reader.IsDBNull(10) ? null : reader.GetString(10),
                            color = reader.IsDBNull(11) ? null : reader.GetString(11),
                            photoUrl = (string)null
                        });
                    }
                }
            }

            Console.WriteLine($"✅ Found {products.Count} active products");

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Write JSON
            var json = JsonSerializer.Serialize(products, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            File.WriteAllText(outputPath, json);

            Console.WriteLine($"✅ Products exported to: {outputPath}");
            Console.WriteLine("🎉 Done!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }

    static string FindDatabase()
    {
        var searchPaths = new[]
        {
            // Production publish directory (most common)
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "publish", "Database", "KosovaPOS.db"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "publish", "production", "Database", "KosovaPOS.db"),
            // From Tools/ExportProducts going up to POS root
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "bin", "Debug", "net8.0-windows", "win-x64", "Database", "KosovaPOS.db"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "bin", "Release", "net8.0-windows", "win-x64", "Database", "KosovaPOS.db"),
            // Older paths
            Path.Combine(Directory.GetCurrentDirectory(), "..", "bin", "Debug", "net8.0-windows", "win-x64", "Database", "KosovaPOS.db"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "bin", "Release", "net8.0-windows", "win-x64", "Database", "KosovaPOS.db"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "Database", "KosovaPOS.db"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Database", "KosovaPOS.db")
        };

        foreach (var path in searchPaths)
        {
            var fullPath = Path.GetFullPath(path);
            Console.WriteLine($"Checking: {fullPath}");
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }
}
