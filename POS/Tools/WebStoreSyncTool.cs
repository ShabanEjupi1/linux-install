using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace KosovaPOS.Tools
{
    /// <summary>
    /// Syncs products from KosovaPOS database to products.json for web store
    /// </summary>
    public class WebStoreSyncTool
    {
        public static void SyncProducts(string databasePath, string outputPath)
        {
            Console.WriteLine("🔄 Starting product sync to web store...");
            Console.WriteLine($"📁 Database: {databasePath}");
            Console.WriteLine($"📁 Output: {outputPath}");

            if (!File.Exists(databasePath))
            {
                Console.WriteLine($"❌ Database not found: {databasePath}");
                return;
            }

            try
            {
                var products = new List<Dictionary<string, object>>();

                using var connection = new SqliteConnection($"Data Source={databasePath}");
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT 
                        Id, Barcode, Name, Unit, SalesPrice, Category, Brand, 
                        Supplier, StockQuantity, PhotoPath, Size, Color
                    FROM Articles 
                    WHERE IsActive = 1
                    ORDER BY Name";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var product = new Dictionary<string, object>
                    {
                        ["id"] = reader.GetInt32(0),
                        ["barcode"] = reader.IsDBNull(1) ? null : reader.GetString(1),
                        ["name"] = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        ["unit"] = reader.IsDBNull(3) ? "Copë" : reader.GetString(3),
                        ["salesPrice"] = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4),
                        ["category"] = reader.IsDBNull(5) ? null : reader.GetString(5),
                        ["brand"] = reader.IsDBNull(6) ? null : reader.GetString(6),
                        ["supplier"] = reader.IsDBNull(7) ? null : reader.GetString(7),
                        ["stockQuantity"] = reader.IsDBNull(8) ? 0m : reader.GetDecimal(8),
                        ["photoPath"] = reader.IsDBNull(9) ? null : reader.GetString(9),
                        ["size"] = reader.IsDBNull(10) ? null : reader.GetString(10),
                        ["color"] = reader.IsDBNull(11) ? null : reader.GetString(11),
                        ["photoUrl"] = (object)null // Will be populated from photos.json
                    };
                    products.Add(product);
                }

                Console.WriteLine($"✅ Found {products.Count} active products");

                // Load existing photos if any
                var photosPath = Path.Combine(Path.GetDirectoryName(outputPath), "photos.json");
                if (File.Exists(photosPath))
                {
                    var photosJson = File.ReadAllText(photosPath);
                    var photos = JsonSerializer.Deserialize<Dictionary<string, PhotoInfo>>(photosJson);
                    
                    foreach (var product in products)
                    {
                        var id = product["id"].ToString();
                        if (photos.TryGetValue(id, out var photoInfo))
                        {
                            product["photoUrl"] = photoInfo.Url;
                        }
                    }
                }

                // Ensure output directory exists
                var outputDir = Path.GetDirectoryName(outputPath);
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // Write products.json
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var json = JsonSerializer.Serialize(products, options);
                File.WriteAllText(outputPath, json);

                Console.WriteLine($"✅ Products saved to: {outputPath}");
                Console.WriteLine("🎉 Sync complete!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        private class PhotoInfo
        {
            public string Url { get; set; }
            public string Filename { get; set; }
            public string UploadedAt { get; set; }
        }
    }
}
