using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace KosovaPOS.Tools
{
    /// <summary>
    /// Safe utility to fix stock quantities from the original SQL script.
    /// This tool ONLY updates StockQuantity, StockIn, and StockOut fields.
    /// It does NOT modify any other data.
    /// 
    /// Usage: Run from command line with path to script.sql and database path
    /// </summary>
    public class FixStockQuantities
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║     STOCK QUANTITY FIXER - Safe Database Update Tool              ║");
            Console.WriteLine("║     This tool ONLY updates stock quantities, nothing else!        ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            // Default paths
            string scriptPath = args.Length > 0 
                ? args[0] 
                : @"C:\Users\Dell\Desktop\POS\script.sql";
            
            string dbPath = args.Length > 1 
                ? args[1] 
                : @"C:\Users\Dell\Desktop\POS\publish\production\Database\KosovaPOS.db";

            if (!File.Exists(scriptPath))
            {
                Console.WriteLine($"❌ Script file not found: {scriptPath}");
                Console.WriteLine("Usage: FixStockQuantities.exe <script.sql path> <database path>");
                return;
            }

            if (!File.Exists(dbPath))
            {
                Console.WriteLine($"❌ Database not found: {dbPath}");
                Console.WriteLine("Usage: FixStockQuantities.exe <script.sql path> <database path>");
                return;
            }

            Console.WriteLine($"📄 Script: {scriptPath}");
            Console.WriteLine($"📦 Database: {dbPath}");
            Console.WriteLine();

            // Create backup first
            string backupPath = dbPath + ".backup_stock_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Console.WriteLine($"💾 Creating backup: {backupPath}");
            File.Copy(dbPath, backupPath, overwrite: false);
            Console.WriteLine("✅ Backup created successfully");
            Console.WriteLine();

            try
            {
                // Step 1: Extract stock data from SQL script
                Console.WriteLine("📊 Step 1: Extracting stock data from SQL script...");
                var stockData = ExtractStockDataFromScript(scriptPath);
                Console.WriteLine($"✅ Found {stockData.Count} articles with stock data");
                Console.WriteLine();

                // Step 2: Update database
                Console.WriteLine("📝 Step 2: Updating database (ONLY stock quantities)...");
                int updated = UpdateStockQuantities(dbPath, stockData);
                Console.WriteLine($"✅ Updated {updated} articles");
                Console.WriteLine();

                // Step 3: Verify
                Console.WriteLine("🔍 Step 3: Verifying update...");
                VerifyUpdate(dbPath, stockData);

                Console.WriteLine();
                Console.WriteLine("═══════════════════════════════════════════════════════════════════");
                Console.WriteLine("✅ STOCK UPDATE COMPLETE!");
                Console.WriteLine("═══════════════════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("Restoring from backup...");
                File.Copy(backupPath, dbPath, overwrite: true);
                Console.WriteLine("✅ Database restored from backup");
            }
        }

        private static Dictionary<string, StockInfo> ExtractStockDataFromScript(string scriptPath)
        {
            var stockData = new Dictionary<string, StockInfo>(StringComparer.OrdinalIgnoreCase);
            
            var insertPattern = @"INSERT \[dbo\]\.\[Artikujt\].*?VALUES \((.*?)\)";
            
            using var reader = new StreamReader(scriptPath);
            string line;
            string currentInsert = "";
            
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Contains("INSERT [dbo].[Artikujt]"))
                {
                    currentInsert = line;
                    
                    if (currentInsert.Contains(") VALUES (") && currentInsert.TrimEnd().EndsWith(")"))
                    {
                        ProcessInsert(currentInsert, stockData);
                        currentInsert = "";
                    }
                }
                else if (!string.IsNullOrEmpty(currentInsert))
                {
                    currentInsert += " " + line.Trim();
                    
                    if (currentInsert.TrimEnd().EndsWith(")"))
                    {
                        ProcessInsert(currentInsert, stockData);
                        currentInsert = "";
                    }
                }
            }
            
            return stockData;
        }

        private static void ProcessInsert(string insertStatement, Dictionary<string, StockInfo> stockData)
        {
            try
            {
                var valuesMatch = Regex.Match(insertStatement, @"VALUES \((.*)\)");
                if (!valuesMatch.Success) return;
                
                var values = valuesMatch.Groups[1].Value;
                var parts = SplitSqlValues(values);
                
                if (parts.Count < 23) return;
                
                // Column mapping:
                // 1=Barkodi (Barcode), 15=Sasia (Stock Quantity), 21=SasiaHyrje (Stock In), 22=SasiaDalje (Stock Out)
                
                string barcode = CleanSqlString(parts[1])?.Trim();
                if (string.IsNullOrEmpty(barcode)) return;
                
                var info = new StockInfo
                {
                    Barcode = barcode,
                    StockQuantity = ParseDecimal(parts[15]),
                    StockIn = ParseDecimal(parts[21]),
                    StockOut = ParseDecimal(parts[22])
                };
                
                // Only add if there's meaningful stock data
                if (info.StockQuantity != 0 || info.StockIn != 0 || info.StockOut != 0)
                {
                    stockData[barcode] = info;
                }
            }
            catch { }
        }

        private static int UpdateStockQuantities(string dbPath, Dictionary<string, StockInfo> stockData)
        {
            int updated = 0;
            
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            
            using var transaction = connection.BeginTransaction();
            
            foreach (var kvp in stockData)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    UPDATE Articles 
                    SET StockQuantity = @stockQty,
                        StockIn = @stockIn,
                        StockOut = @stockOut,
                        UpdatedAt = @updatedAt
                    WHERE Barcode = @barcode";
                
                command.Parameters.AddWithValue("@barcode", kvp.Key);
                command.Parameters.AddWithValue("@stockQty", kvp.Value.StockQuantity);
                command.Parameters.AddWithValue("@stockIn", kvp.Value.StockIn);
                command.Parameters.AddWithValue("@stockOut", kvp.Value.StockOut);
                command.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                
                int affected = command.ExecuteNonQuery();
                if (affected > 0)
                {
                    updated++;
                }
            }
            
            transaction.Commit();
            
            return updated;
        }

        private static void VerifyUpdate(string dbPath, Dictionary<string, StockInfo> stockData)
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            
            // Count articles with non-zero stock
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Articles WHERE StockQuantity > 0";
            var count = Convert.ToInt32(command.ExecuteScalar());
            
            Console.WriteLine($"   Articles with stock > 0: {count}");
            
            // Show top 5 samples
            command.CommandText = @"
                SELECT Barcode, Name, StockQuantity, StockIn, StockOut 
                FROM Articles 
                WHERE StockQuantity > 0 
                ORDER BY StockQuantity DESC 
                LIMIT 5";
            
            using var reader = command.ExecuteReader();
            Console.WriteLine("   Sample articles with highest stock:");
            while (reader.Read())
            {
                Console.WriteLine($"     - {reader.GetString(0)}: {reader.GetString(1)} - Stock: {reader.GetDecimal(2)}");
            }
        }

        private static List<string> SplitSqlValues(string values)
        {
            var parts = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;
            int parenDepth = 0;
            
            for (int i = 0; i < values.Length; i++)
            {
                char c = values[i];
                
                if (c == '\'' && (i == 0 || values[i-1] != '\\'))
                {
                    inQuotes = !inQuotes;
                    current.Append(c);
                }
                else if (c == '(' && !inQuotes)
                {
                    parenDepth++;
                    current.Append(c);
                }
                else if (c == ')' && !inQuotes)
                {
                    parenDepth--;
                    current.Append(c);
                }
                else if (c == ',' && !inQuotes && parenDepth == 0)
                {
                    parts.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            
            if (current.Length > 0)
                parts.Add(current.ToString().Trim());
            
            return parts;
        }

        private static string CleanSqlString(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            value = value.Trim();
            if (value.Equals("NULL", StringComparison.OrdinalIgnoreCase)) return null;
            if (value.StartsWith("N'") && value.EndsWith("'"))
                value = value.Substring(2, value.Length - 3);
            else if (value.StartsWith("'") && value.EndsWith("'"))
                value = value.Substring(1, value.Length - 2);
            value = value.Replace("''", "'");
            return value;
        }

        private static decimal ParseDecimal(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            value = value.Trim().Replace("CAST(", "").Replace(" AS Decimal(18, 2))", "").Replace(" AS Decimal(18, 4))", "");
            if (value.Equals("NULL", StringComparison.OrdinalIgnoreCase)) return 0;
            if (decimal.TryParse(value, System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out var result))
                return result;
            return 0;
        }

        private class StockInfo
        {
            public string Barcode { get; set; }
            public decimal StockQuantity { get; set; }
            public decimal StockIn { get; set; }
            public decimal StockOut { get; set; }
        }
    }
}
