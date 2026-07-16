using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KosovaPOS.Database;
using KosovaPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS.Tools
{
    /// <summary>
    /// Simple CSV-based PLU mapper when DBF access is not available
    /// </summary>
    public class PLUMapperCSV
    {
        public class FiscalArticle
        {
            public int PLU { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Barcode { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public decimal Price { get; set; }
        }
        
        /// <summary>
        /// Read fiscal articles from CSV file
        /// Expected format: PLU,CODE,BARCODE,NAME,PRICE
        /// </summary>
        public static List<FiscalArticle> ReadFromCSV(string csvPath)
        {
            var articles = new List<FiscalArticle>();
            
            if (!File.Exists(csvPath))
            {
                throw new FileNotFoundException($"CSV file not found: {csvPath}");
            }
            
            var lines = File.ReadAllLines(csvPath, Encoding.UTF8);
            
            // Skip header line
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                
                var parts = line.Split('\t');
                if (parts.Length < 5)
                    continue;
                
                try
                {
                    var article = new FiscalArticle
                    {
                        PLU = int.Parse(parts[0]),
                        Code = parts[1].Trim(),
                        Barcode = parts[2].Trim(),
                        Name = parts[3].Trim(),
                        Price = decimal.Parse(parts[4])
                    };
                    
                    articles.Add(article);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not parse line {i}: {line} - {ex.Message}");
                }
            }
            
            Console.WriteLine($"Read {articles.Count} fiscal articles from CSV");
            return articles;
        }
        
        /// <summary>
        /// Map fiscal articles from CSV to database
        /// </summary>
        public static void MapFromCSV(string csvPath, string connectionString = "Data Source=pos.db")
        {
            var fiscalArticles = ReadFromCSV(csvPath);
            
            if (fiscalArticles.Count == 0)
            {
                Console.WriteLine("No fiscal articles found in CSV. Cannot map PLU numbers.");
                return;
            }
            
            Console.WriteLine($"\nMapping {fiscalArticles.Count} fiscal articles to POS database...\n");
            
            var optionsBuilder = new DbContextOptionsBuilder<POSDbContext>();
            optionsBuilder.UseSqlite(connectionString);
            
            using (var context = new POSDbContext(optionsBuilder.Options))
            {
                var dbArticles = context.Articles.ToList();
                Console.WriteLine($"Found {dbArticles.Count} articles in POS database");
                
                int matched = 0;
                int updated = 0;
                int notFound = 0;
                var unmatchedFiscal = new List<FiscalArticle>();
                
                foreach (var fiscalArticle in fiscalArticles)
                {
                    // Try to match by exact name first
                    var dbArticle = dbArticles.FirstOrDefault(a =>
                        NormalizeName(a.Name).Equals(NormalizeName(fiscalArticle.Name), StringComparison.OrdinalIgnoreCase));
                    
                    // If not found, try by barcode
                    if (dbArticle == null && !string.IsNullOrWhiteSpace(fiscalArticle.Barcode) && fiscalArticle.Barcode != "0")
                    {
                        dbArticle = dbArticles.FirstOrDefault(a =>
                            a.Barcode.Equals(fiscalArticle.Barcode, StringComparison.OrdinalIgnoreCase));
                    }
                    
                    // If still not found, try by code
                    if (dbArticle == null && !string.IsNullOrWhiteSpace(fiscalArticle.Code) && fiscalArticle.Code != "0")
                    {
                        dbArticle = dbArticles.FirstOrDefault(a =>
                            a.Barcode.Equals(fiscalArticle.Code, StringComparison.OrdinalIgnoreCase));
                    }
                    
                    // Try fuzzy matching (partial name match)
                    if (dbArticle == null)
                    {
                        var normalizedFiscalName = NormalizeName(fiscalArticle.Name);
                        dbArticle = dbArticles.FirstOrDefault(a =>
                        {
                            var normalizedDbName = NormalizeName(a.Name);
                            return normalizedDbName.Contains(normalizedFiscalName) ||
                                   normalizedFiscalName.Contains(normalizedDbName);
                        });
                    }
                    
                    if (dbArticle != null)
                    {
                        matched++;
                        
                        if (dbArticle.PLU != fiscalArticle.PLU)
                        {
                            Console.WriteLine($"✓ Matched: '{dbArticle.Name}' -> PLU {fiscalArticle.PLU} (was: {dbArticle.PLU?.ToString() ?? "null"})");
                            dbArticle.PLU = fiscalArticle.PLU;
                            updated++;
                        }
                        else
                        {
                            Console.WriteLine($"  Already mapped: '{dbArticle.Name}' -> PLU {fiscalArticle.PLU}");
                        }
                    }
                    else
                    {
                        notFound++;
                        unmatchedFiscal.Add(fiscalArticle);
                    }
                }
                
                // Save changes
                if (updated > 0)
                {
                    context.SaveChanges();
                    Console.WriteLine($"\n✓ Successfully updated {updated} PLU mappings in database");
                }
                else
                {
                    Console.WriteLine($"\nNo updates needed. All PLU mappings are already correct.");
                }
                
                Console.WriteLine($"\nSummary:");
                Console.WriteLine($"  - Matched: {matched}");
                Console.WriteLine($"  - Updated: {updated}");
                Console.WriteLine($"  - Not found in DB: {notFound}");
                
                if (unmatchedFiscal.Count > 0)
                {
                    Console.WriteLine($"\n⚠️ {unmatchedFiscal.Count} fiscal articles could not be matched:");
                    foreach (var fa in unmatchedFiscal.Take(20))
                    {
                        Console.WriteLine($"  - PLU {fa.PLU}: {fa.Name}");
                    }
                    if (unmatchedFiscal.Count > 20)
                    {
                        Console.WriteLine($"  ... and {unmatchedFiscal.Count - 20} more");
                    }
                }
            }
        }
        
        private static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;
            
            // Remove special characters, extra spaces, and make lowercase
            name = name.Trim().ToLowerInvariant();
            
            // Remove common suffixes/prefixes that might differ
            var patterns = new[] { "~1", "~2", "~3", "~4", "~5", "~6", "~7", "~8", "~9", "~10", "~11" };
            foreach (var pattern in patterns)
            {
                name = name.Replace(pattern, "");
            }
            
            // Normalize multiple spaces to single space
            while (name.Contains("  "))
                name = name.Replace("  ", " ");
            
            return name.Trim();
        }
    }
}
