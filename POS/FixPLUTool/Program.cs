using System;
using System.Linq;
using KosovaPOS.Database;
using KosovaPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace FixPLUTool
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("===========================================");
            Console.WriteLine("  Fiscal Receipt PLU Data Fixer");
            Console.WriteLine("===========================================");
            Console.WriteLine();
            Console.WriteLine("This tool extracts PLU codes from historical receipts");
            Console.WriteLine("(2019+) and updates the Articles table so fiscal receipts");
            Console.WriteLine("can print properly.");
            Console.WriteLine();

            // Set database path - try common locations
            var possiblePaths = new[]
            {
                @"C:\Users\Dell\Desktop\POS\Database\KosovaPOS.db",  // Development database (priority)
                @"C:\Users\Dell\Desktop\POS\publish\Database\KosovaPOS.db",  // Published database
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Database", "KosovaPOS.db"),
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "publish", "Database", "KosovaPOS.db"),
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "bin", "Debug", "net8.0-windows", "Database", "KosovaPOS.db"),
            };

            string? dbPath = null;
            foreach (var path in possiblePaths)
            {
                var fullPath = System.IO.Path.GetFullPath(path);
                if (System.IO.File.Exists(fullPath))
                {
                    dbPath = fullPath;
                    break;
                }
            }

            if (dbPath == null)
            {
                Console.WriteLine("ERROR: Database file not found!");
                Console.WriteLine("Searched locations:");
                foreach (var path in possiblePaths)
                {
                    Console.WriteLine($"  - {System.IO.Path.GetFullPath(path)}");
                }
                Console.WriteLine();
                Console.WriteLine("Please ensure KosovaPOS.db exists in one of these locations.");
                return;
            }

            Console.WriteLine($"Database: {dbPath}");
            Console.WriteLine();

            // Set environment variable for POSDbContext to use
            Environment.SetEnvironmentVariable("DATABASE_PATH", dbPath);

            using (var db = new POSDbContext())
            {
                Console.WriteLine("Step 1: Analyzing Articles table...");
                var totalArticles = db.Articles.Count();
                var articlesWithPLU = db.Articles.Count(a => a.PLU != null && a.PLU > 0);
                var articlesWithoutPLU = totalArticles - articlesWithPLU;
                
                Console.WriteLine($"  Total Articles: {totalArticles}");
                Console.WriteLine($"  Articles with PLU: {articlesWithPLU}");
                Console.WriteLine($"  Articles WITHOUT PLU: {articlesWithoutPLU}");
                Console.WriteLine();

                // Step 2: Check receipts from 2019 onwards
                var startDate = new DateTime(2019, 1, 1);
                var totalReceipts = db.Receipts.Count(r => r.Date >= startDate);
                
                Console.WriteLine("Step 2: Analyzing historical receipts (2019+)...");
                Console.WriteLine($"  Total Receipts since 2019: {totalReceipts}");
                
                if (totalReceipts == 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("ERROR: No receipts found from 2019 onwards!");
                    Console.WriteLine("Cannot extract PLU codes from empty receipt history.");
                    return;
                }

                // Count receipt items with PLU
                var receiptItemsWithPLU = db.ReceiptItems
                    .Where(ri => ri.Receipt != null && ri.Receipt.Date >= startDate && ri.PLU > 0)
                    .ToList();
                
                Console.WriteLine($"  Receipt Items with PLU codes: {receiptItemsWithPLU.Count}");
                Console.WriteLine();

                if (receiptItemsWithPLU.Count == 0)
                {
                    Console.WriteLine("ERROR: No receipt items found with PLU codes!");
                    Console.WriteLine("This could mean:");
                    Console.WriteLine("  1. PLU codes were never stored in ReceiptItems");
                    Console.WriteLine("  2. The PLU column in ReceiptItems table is empty");
                    Console.WriteLine();
                    Console.WriteLine("Checking if PLU column exists in ReceiptItems...");
                    
                    // Try to query for any receipt items
                    var anyReceiptItems = db.ReceiptItems.Take(5).ToList();
                    Console.WriteLine($"Sample receipt items found: {anyReceiptItems.Count}");
                    foreach (var item in anyReceiptItems)
                    {
                        Console.WriteLine($"  - Barcode: {item.Barcode}, Name: {item.ArticleName}, PLU: {item.PLU}");
                    }
                    
                    return;
                }
                
                // Step 3: Group by barcode and PLU to create mapping
                Console.WriteLine("Step 3: Creating barcode -> PLU mapping from receipts...");
                
                var barcodePLUMapping = receiptItemsWithPLU
                    .GroupBy(ri => ri.Barcode)
                    .Select(g => new
                    {
                        Barcode = g.Key,
                        PLU = g.OrderByDescending(ri => ri.Receipt!.Date).First().PLU,  // Use most recent PLU
                        ArticleName = g.First().ArticleName,
                        Count = g.Count(),
                        FirstUsed = g.Min(ri => ri.Receipt!.Date),
                        LastUsed = g.Max(ri => ri.Receipt!.Date)
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList();

                Console.WriteLine($"  Found {barcodePLUMapping.Count} unique barcode->PLU mappings");
                Console.WriteLine();

                // Display sample mappings
                Console.WriteLine("Sample barcode->PLU mappings (top 20 most used):");
                Console.WriteLine("--------------------------------------------------------------------------------");
                Console.WriteLine($"{"Barcode",-15} {"PLU",-6} {"Name",-35} {"Times",-8} {"Last Used"}");
                Console.WriteLine("--------------------------------------------------------------------------------");
                
                foreach (var mapping in barcodePLUMapping.Take(20))
                {
                    Console.WriteLine($"{mapping.Barcode,-15} {mapping.PLU,-6} {mapping.ArticleName.Substring(0, Math.Min(35, mapping.ArticleName.Length)),-35} {mapping.Count,-8} {mapping.LastUsed:yyyy-MM-dd}");
                }
                
                if (barcodePLUMapping.Count > 20)
                {
                    Console.WriteLine($"... and {barcodePLUMapping.Count - 20} more");
                }
                Console.WriteLine("--------------------------------------------------------------------------------");
                Console.WriteLine();

                // Step 4: Update Articles table with PLU codes
                Console.WriteLine("Step 4: Ready to update Articles table with PLU codes");
                Console.WriteLine();
                Console.WriteLine("This will:");
                Console.WriteLine("  1. Match articles by barcode with receipt items");
                Console.WriteLine("  2. Copy PLU codes from receipts to articles");
                Console.WriteLine("  3. Update the database");
                Console.WriteLine();
                Console.Write("Do you want to proceed? (yes/no): ");
                
                var confirmation = Console.ReadLine()?.Trim().ToLower();

                if (confirmation != "yes" && confirmation != "y")
                {
                    Console.WriteLine();
                    Console.WriteLine("Update cancelled by user.");
                    return;
                }

                Console.WriteLine();
                Console.WriteLine("Updating articles...");
                
                int updatedCount = 0;
                int alreadyCorrect = 0;
                int notFoundCount = 0;

                foreach (var mapping in barcodePLUMapping)
                {
                    var article = db.Articles.FirstOrDefault(a => a.Barcode == mapping.Barcode);
                    
                    if (article == null)
                    {
                        notFoundCount++;
                        Console.WriteLine($"  WARNING: Article with barcode '{mapping.Barcode}' not found in Articles table");
                        continue;
                    }

                    if (article.PLU != null && article.PLU == mapping.PLU)
                    {
                        alreadyCorrect++;
                        continue;
                    }

                    if (article.PLU != null && article.PLU > 0 && article.PLU != mapping.PLU)
                    {
                        Console.WriteLine($"  CONFLICT: Article '{article.Name}' has PLU {article.PLU}, but receipts use PLU {mapping.PLU}. Updating to match receipts...");
                    }
                    else
                    {
                        Console.WriteLine($"  ✓ Updating: {mapping.Barcode,-15} '{article.Name.Substring(0, Math.Min(30, article.Name.Length)),-30}' -> PLU {mapping.PLU}");
                    }
                    
                    article.PLU = mapping.PLU;
                    updatedCount++;
                }

                // Save changes
                if (updatedCount > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("Saving changes to database...");
                    db.SaveChanges();
                    Console.WriteLine($"✓ Successfully updated {updatedCount} articles with PLU codes");
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("No articles needed updating.");
                }

                Console.WriteLine($"  Articles already had correct PLU: {alreadyCorrect}");
                
                if (notFoundCount > 0)
                {
                    Console.WriteLine($"  WARNING: {notFoundCount} barcodes from receipts not found in Articles table");
                    Console.WriteLine("           These may be old/deleted articles or need to be added.");
                }
                
                Console.WriteLine();

                // Step 5: Final verification
                Console.WriteLine("Step 5: Final verification...");
                articlesWithPLU = db.Articles.Count(a => a.PLU != null && a.PLU > 0);
                articlesWithoutPLU = totalArticles - articlesWithPLU;
                
                Console.WriteLine($"  Articles WITH PLU: {articlesWithPLU}");
                Console.WriteLine($"  Articles WITHOUT PLU: {articlesWithoutPLU}");
                Console.WriteLine();

                if (articlesWithoutPLU > 0)
                {
                    Console.WriteLine($"There are still {articlesWithoutPLU} articles without PLU codes.");
                    Console.WriteLine("These articles have likely never been sold in a receipt.");
                    Console.WriteLine();
                    Console.WriteLine("Showing first 10 articles without PLU:");
                    
                    var articlesNeedingPLU = db.Articles
                        .Where(a => a.PLU == null || a.PLU == 0)
                        .Take(10)
                        .ToList();
                    
                    foreach (var article in articlesNeedingPLU)
                    {
                        Console.WriteLine($"  - {article.Barcode,-15} {article.Name}");
                    }
                    
                    if (articlesWithoutPLU > 10)
                    {
                        Console.WriteLine($"  ... and {articlesWithoutPLU - 10} more");
                    }
                    
                    Console.WriteLine();
                    Console.WriteLine("For these articles, you should:");
                    Console.WriteLine("  1. Assign PLU codes manually in the Articles window");
                    Console.WriteLine("  2. Or map them using the PLU Mapper tool");
                    Console.WriteLine("  3. Or remove them if they're no longer needed");
                }

                Console.WriteLine();
                Console.WriteLine("===========================================");
                Console.WriteLine("  PLU Data Fix Complete!");
                Console.WriteLine("===========================================");
                Console.WriteLine();
                Console.WriteLine("Next steps:");
                Console.WriteLine("1. Test printing a fiscal receipt");
                Console.WriteLine("2. Check the fiscal printer log for any errors");
                Console.WriteLine("3. If still having issues, check:");
                Console.WriteLine("   - Fiscal printer is connected to correct COM port");
                Console.WriteLine("   - Fiscal printer driver is running");
                Console.WriteLine("   - FISCAL_INLINE_ARTICLES=true in settings (to avoid Error #11)");
                Console.WriteLine();
            }
        }
    }
}
