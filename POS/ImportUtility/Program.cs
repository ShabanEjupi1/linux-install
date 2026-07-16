using System;
using System.IO;
using System.Threading.Tasks;
using KosovaPOS.Database;
using KosovaPOS.Helpers;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS.Import
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║          Kosovo POS - Database Import Utility (CLI)                 ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            string scriptPath = "../script.sql";
            
            // Check if script path is provided as argument
            if (args.Length > 0)
            {
                scriptPath = args[0];
            }

            // Verify file exists
            if (!File.Exists(scriptPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Error: Script file not found: {scriptPath}");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("Usage: dotnet run [script-path]");
                Console.WriteLine("Example: dotnet run ../script.sql");
                return 1;
            }

            var fileInfo = new FileInfo(scriptPath);
            Console.WriteLine($"📄 Script file: {Path.GetFullPath(scriptPath)}");
            Console.WriteLine($"📊 File size: {fileInfo.Length / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine();

            // Ask for confirmation to clear existing data
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠️  WARNING: This will DELETE all existing data in the database!");
            Console.ResetColor();
            Console.Write("Do you want to continue? (yes/no): ");
            
            var response = Console.ReadLine()?.Trim().ToLower();
            if (response != "yes" && response != "y")
            {
                Console.WriteLine("Import cancelled.");
                return 0;
            }

            Console.WriteLine();
            Console.WriteLine("Starting comprehensive import process...");
            Console.WriteLine();

            try
            {
                // Set database path to parent directory
                Environment.SetEnvironmentVariable("DATABASE_PATH", "../Database/KosovaPOS.db");
                
                // Initialize database context
                using var context = new POSDbContext();
                
                // Ensure database is created
                Console.WriteLine("🔧 Ensuring database exists...");
                await context.Database.EnsureCreatedAsync();
                
                // Clear existing data
                Console.WriteLine("🗑️  Clearing existing data...");
                await context.Database.ExecuteSqlRawAsync("DELETE FROM ReceiptItems");
                await context.Database.ExecuteSqlRawAsync("DELETE FROM Receipts");
                await context.Database.ExecuteSqlRawAsync("DELETE FROM PurchaseItems");
                await context.Database.ExecuteSqlRawAsync("DELETE FROM Purchases");
                await context.Database.ExecuteSqlRawAsync("DELETE FROM Articles");
                await context.Database.ExecuteSqlRawAsync("DELETE FROM BusinessPartners");
                await context.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name IN ('Articles', 'BusinessPartners', 'Purchases', 'PurchaseItems', 'Receipts', 'ReceiptItems')");
                Console.WriteLine("✓ Existing data cleared.");
                Console.WriteLine();

                // Create importer
                var importer = new SqlServerToSQLiteImporter(context);
                
                // Import all data
                Console.WriteLine("📥 Importing data from SQL script...");
                Console.WriteLine("─────────────────────────────────────────────────────────────────────");
                
                var startTime = DateTime.Now;
                var (articlesCount, partnersCount, purchasesCount, receiptsCount) = await importer.ImportAllDataFromSqlScript(scriptPath);
                var endTime = DateTime.Now;
                var duration = endTime - startTime;
                
                Console.WriteLine("─────────────────────────────────────────────────────────────────────");
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Import completed successfully!");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine($"📊 Statistics:");
                Console.WriteLine($"   • Total articles imported: {articlesCount:N0}");
                Console.WriteLine($"   • Total business partners imported: {partnersCount:N0}");
                Console.WriteLine($"   • Total purchase documents imported: {purchasesCount:N0}");
                Console.WriteLine($"   • Total sales receipts imported: {receiptsCount:N0}");
                Console.WriteLine($"   • Duration: {duration.TotalMinutes:F2} minutes ({duration.TotalSeconds:F1} seconds)");
                if (duration.TotalSeconds > 0)
                {
                    var totalRecords = articlesCount + partnersCount + purchasesCount + receiptsCount;
                    Console.WriteLine($"   • Average speed: {totalRecords / duration.TotalSeconds:F1} records/second");
                }
                Console.WriteLine();
                
                // Verify import
                var articlesInDb = await context.Articles.CountAsync();
                var partnersInDb = await context.BusinessPartners.CountAsync();
                var purchasesInDb = await context.Purchases.CountAsync();
                var receiptsInDb = await context.Receipts.CountAsync();
                
                Console.WriteLine($"🔍 Verification:");
                Console.WriteLine($"   • Articles in database: {articlesInDb:N0}");
                Console.WriteLine($"   • Business partners in database: {partnersInDb:N0}");
                Console.WriteLine($"   • Purchase documents in database: {purchasesInDb:N0}");
                Console.WriteLine($"   • Sales receipts in database: {receiptsInDb:N0}");
                
                if (articlesInDb == articlesCount && partnersInDb == partnersCount && 
                    purchasesInDb == purchasesCount && receiptsInDb == receiptsCount)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("   ✓ All data saved successfully!");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"   ⚠️  Warning: Some discrepancies found");
                    Console.ResetColor();
                }
                
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("✓ Database import completed. You can now run the POS application.");
                Console.ResetColor();
                
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Error during import:");
                Console.WriteLine($"   {ex.Message}");
                Console.ResetColor();
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine();
                    Console.WriteLine("Inner exception:");
                    Console.WriteLine($"   {ex.InnerException.Message}");
                    
                    if (ex.InnerException.InnerException != null)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Detailed error:");
                        Console.WriteLine($"   {ex.InnerException.InnerException.Message}");
                    }
                }
                
                Console.WriteLine();
                Console.WriteLine("Stack trace:");
                Console.WriteLine(ex.StackTrace);
                
                return 1;
            }
        }
    }
}
