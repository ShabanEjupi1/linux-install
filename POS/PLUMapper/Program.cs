using System;
using System.IO;
using KosovaPOS.Tools;

namespace PLUMapper
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("===========================================");
            Console.WriteLine("   PLU Mapper - Fiscal Printer Integration");
            Console.WriteLine("===========================================\n");
            
            // Default path to fiscal printer's Articles.dbf
            var defaultDbfPath = @"C:\Program Files (x86)\ENTERNET\PGM-KS\Data\FP550_EN21003910\Articles.dbf";
            var dbfPath = args.Length > 0 ? args[0] : defaultDbfPath;
            
            // Database connection string
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "pos.db");
            var connectionString = $"Data Source={dbPath}";
            
            Console.WriteLine($"Fiscal printer DBF path: {dbfPath}");
            Console.WriteLine($"POS Database path: {Path.GetFullPath(dbPath)}\n");
            
            if (!File.Exists(dbfPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: Articles.dbf not found at: {dbfPath}");
                Console.ResetColor();
                Console.WriteLine("\nUsage: PLUMapper.exe [path_to_Articles.dbf]");
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
                return;
            }
            
            if (!File.Exists(Path.GetFullPath(dbPath)))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: POS database not found at: {Path.GetFullPath(dbPath)}");
                Console.ResetColor();
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
                return;
            }
            
            try
            {
                var mapper = new KosovaPOS.Tools.PLUMapper(dbfPath);
                
                Console.WriteLine("\n[1/3] Reading fiscal printer articles...\n");
                var fiscalArticles = mapper.ReadFiscalArticles();
                
                if (fiscalArticles.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("WARNING: No articles found in fiscal printer database.");
                    Console.ResetColor();
                    Console.WriteLine("\nPress any key to exit...");
                    Console.ReadKey();
                    return;
                }
                
                Console.WriteLine($"\n✓ Successfully read {fiscalArticles.Count} articles from fiscal printer");
                
                // Show some examples
                Console.WriteLine("\nExample articles from fiscal printer:");
                foreach (var article in fiscalArticles.Take(5))
                {
                    Console.WriteLine($"  PLU {article.PLU}: {article.Name} (Price: {article.Price:F2}€)");
                }
                
                Console.WriteLine("\n[2/3] Mapping PLU numbers to POS database...\n");
                mapper.MapPLUToDatabase(connectionString);
                
                Console.WriteLine("\n[3/3] Exporting mapping report...\n");
                var reportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PLU_Mapping_Report.csv");
                mapper.ExportMappingReport(reportPath);
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n✓✓✓ PLU mapping completed successfully! ✓✓✓");
                Console.ResetColor();
                
                Console.WriteLine("\nNext steps:");
                Console.WriteLine("1. Review the mapping report: PLU_Mapping_Report.csv");
                Console.WriteLine("2. Restart your POS application");
                Console.WriteLine("3. Test printing a receipt with 'Bokserica sprint 03504'");
                Console.WriteLine("4. The fiscal printer should now use PLU 2429 instead of 646");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n✗ ERROR: {ex.Message}");
                Console.WriteLine($"\nDetails: {ex.StackTrace}");
                Console.ResetColor();
            }
            
            Console.WriteLine("\n\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
