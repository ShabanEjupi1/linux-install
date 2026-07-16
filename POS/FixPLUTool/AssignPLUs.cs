using System;
using System.Linq;
using KosovaPOS.Database;
using Microsoft.EntityFrameworkCore;

namespace FixPLUTool
{
    class AssignPLUs
    {
        static void Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("  Auto-Assign PLU Codes");
            Console.WriteLine("========================================");
            Console.WriteLine();
            
            // Set database path
            var dbPath = @"C:\Users\Dell\Desktop\POS\Database\KosovaPOS.db";
            if (!System.IO.File.Exists(dbPath))
            {
                dbPath = @"C:\Users\Dell\Desktop\POS\publish\Database\KosovaPOS.db";
            }
            
            if (!System.IO.File.Exists(dbPath))
            {
                Console.WriteLine("ERROR: Database not found!");
                return;
            }
            
            Environment.SetEnvironmentVariable("DATABASE_PATH", dbPath);
            Console.WriteLine($"Database: {dbPath}");
            Console.WriteLine();

            using (var db = new POSDbContext())
            {
                // Analyze current state
                var totalArticles = db.Articles.Count();
                var withoutPLU = db.Articles.Count(a => a.PLU == null || a.PLU == 0);
                var withPLU = totalArticles - withoutPLU;
                var maxId = db.Articles.Max(a => (int?)a.Id) ?? 0;
                
                Console.WriteLine("CURRENT STATE:");
                Console.WriteLine($"  Total Articles: {totalArticles}");
                Console.WriteLine($"  With PLU: {withPLU}");
                Console.WriteLine($"  Without PLU: {withoutPLU}");
                Console.WriteLine($"  Max Article ID: {maxId}");
                Console.WriteLine();
                
                if (maxId >= 10000)
                {
                    Console.WriteLine("WARNING: Some article IDs are >= 10000!");
                    Console.WriteLine("Fiscal printers typically support PLU codes 1-9999");
                    Console.WriteLine("Articles with high IDs will need manual PLU assignment.");
                    Console.WriteLine();
                }
                
                if (withoutPLU == 0)
                {
                    Console.WriteLine("All articles already have PLU codes!");
                    return;
                }
                
                // Show sample
                Console.WriteLine("SAMPLE - First 10 articles that will be updated:");
                Console.WriteLine("--------------------------------------------------------------------------------");
                Console.WriteLine($"{"ID",-6} {"Barcode",-15} {"Name",-40} {"→ New PLU"}");
                Console.WriteLine("--------------------------------------------------------------------------------");
                
                var samples = db.Articles
                    .Where(a => a.PLU == null || a.PLU == 0)
                    .OrderBy(a => a.Id)
                    .Take(10)
                    .ToList();
                
                foreach (var article in samples)
                {
                    var name = article.Name.Length > 40 ? article.Name.Substring(0, 37) + "..." : article.Name;
                    Console.WriteLine($"{article.Id,-6} {article.Barcode,-15} {name,-40} → PLU {article.Id}");
                }
                
                if (withoutPLU > 10)
                {
                    Console.WriteLine($"... and {withoutPLU - 10} more articles");
                }
                Console.WriteLine("--------------------------------------------------------------------------------");
                Console.WriteLine();
                
                Console.WriteLine("This will assign PLU = Article ID to all articles without PLU codes.");
                Console.WriteLine();
                Console.Write("Do you want to proceed? (yes/no): ");
                
                var response = Console.ReadLine()?.Trim().ToLower();
                if (response != "yes" && response != "y")
                {
                    Console.WriteLine("Cancelled.");
                    return;
                }
                
                Console.WriteLine();
                Console.WriteLine("Updating articles...");
                
                // Perform update
                var articlesToUpdate = db.Articles
                    .Where(a => (a.PLU == null || a.PLU == 0) && a.Id < 10000)
                    .ToList();
                
                int updated = 0;
                foreach (var article in articlesToUpdate)
                {
                    article.PLU = article.Id;
                    updated++;
                    
                    if (updated <= 20 || updated % 100 == 0)
                    {
                        Console.WriteLine($"  ✓ {article.Barcode,-15} {article.Name.Substring(0, Math.Min(40, article.Name.Length)),-40} → PLU {article.PLU}");
                    }
                }
                
                if (updated > 20)
                {
                    Console.WriteLine($"  ... (showing 20 of {updated} updates)");
                }
                
                Console.WriteLine();
                Console.WriteLine("Saving to database...");
                db.SaveChanges();
                
                Console.WriteLine($"✓ Successfully assigned PLU codes to {updated} articles!");
                Console.WriteLine();
                
                // Final verification
                var finalWithPLU = db.Articles.Count(a => a.PLU > 0);
                var finalWithoutPLU = totalArticles - finalWithPLU;
                
                Console.WriteLine("FINAL STATE:");
                Console.WriteLine($"  Articles with PLU: {finalWithPLU}");
                Console.WriteLine($"  Articles without PLU: {finalWithoutPLU}");
                Console.WriteLine();
                
                if (finalWithoutPLU > 0)
                {
                    Console.WriteLine($"NOTE: {finalWithoutPLU} articles still need PLU codes.");
                    Console.WriteLine("These are likely articles with ID >= 10000.");
                    Console.WriteLine("You'll need to assign PLU codes manually for these.");
                    Console.WriteLine();
                    
                    var remaining = db.Articles
                        .Where(a => a.PLU == null || a.PLU == 0)
                        .Take(10)
                        .ToList();
                    
                    Console.WriteLine("Articles still needing PLU:");
                    foreach (var article in remaining)
                    {
                        Console.WriteLine($"  - ID:{article.Id,-6} {article.Barcode,-15} {article.Name}");
                    }
                }
                
                Console.WriteLine();
                Console.WriteLine("========================================");
                Console.WriteLine("  PLU Assignment Complete!");
                Console.WriteLine("========================================");
                Console.WriteLine();
                Console.WriteLine("NEXT STEPS:");
                Console.WriteLine("1. Test printing a fiscal receipt");
                Console.WriteLine("2. The receipt should now print successfully");
                Console.WriteLine("3. Check the fiscal printer log if you encounter errors");
                Console.WriteLine("4. Verify PLU codes appear in printed receipts");
                Console.WriteLine();
            }
        }
    }
}
