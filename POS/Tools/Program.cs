using System;
using System.IO;
using KosovaPOS.Tools;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("PLU Mapper Tool for Kosovo POS");
        Console.WriteLine("================================");
        Console.WriteLine();
        
        try
        {
            // Get paths
            var scriptDir = Directory.GetCurrentDirectory();
            var dbPath = Path.Combine(scriptDir, "Database", "KosovaPOS.db");
            
            // Check if we're running from the build directory
            if (!File.Exists(dbPath))
            {
                // Try going up to the project root
                var projectRoot = Path.GetFullPath(Path.Combine(scriptDir, "..", "..", "..", ".."));
                dbPath = Path.Combine(projectRoot, "Database", "KosovaPOS.db");
            }
            
            if (!File.Exists(dbPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: Database not found at: {dbPath}");
                Console.WriteLine("Please run this tool from the POS directory or ensure Database/KosovaPOS.db exists.");
                Console.ResetColor();
                return;
            }
            
            Console.WriteLine($"Using database: {dbPath}");
            Console.WriteLine();
            
            // Default fiscal printer path
            var articlesDbfPath = @"C:\Program Files (x86)\ENTERNET\PGM-KS\Data\FP550_EN21003910\Articles.dbf";
            var articlesTxtPath = Path.Combine(scriptDir, "FiscalArticles.txt");
            
            // Check if custom path provided as argument
            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                var customPath = args[0];
                if (customPath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                    articlesTxtPath = customPath;
                else
                    articlesDbfPath = customPath;
            }
            
            var connectionString = $"Data Source={dbPath}";
            
            // Try CSV/TXT file first (more reliable, no OLE DB needed)
            if (File.Exists(articlesTxtPath))
            {
                Console.WriteLine($"Using fiscal articles from TXT: {articlesTxtPath}");
                Console.WriteLine();
                
                PLUMapperCSV.MapFromCSV(articlesTxtPath, connectionString);
            }
            else if (File.Exists(articlesDbfPath))
            {
                Console.WriteLine($"Using fiscal articles from DBF: {articlesDbfPath}");
                Console.WriteLine();
                
                // Create PLU mapper and run it
                var mapper = new PLUMapper(articlesDbfPath);
                
                // Map PLU numbers
                mapper.MapPLUToDatabase(connectionString);
                
                // Export report
                var reportPath = Path.Combine(Path.GetDirectoryName(dbPath) ?? ".", "PLU_Mapping_Report.csv");
                mapper.ExportMappingReport(reportPath, connectionString);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: No fiscal articles file found!");
                Console.WriteLine($"  Checked TXT: {articlesTxtPath}");
                Console.WriteLine($"  Checked DBF: {articlesDbfPath}");
                Console.WriteLine();
                Console.WriteLine("Please specify the correct path as an argument:");
                Console.WriteLine("  Tools.exe \"C:\\Path\\To\\FiscalArticles.txt\"");
                Console.WriteLine("  Tools.exe \"C:\\Path\\To\\Articles.dbf\"");
                Console.ResetColor();
                return;
            }
            
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ PLU mapping completed successfully!");
            Console.ResetColor();
        }
        catch (FileNotFoundException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.ResetColor();
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Console.ResetColor();
            Environment.Exit(1);
        }
    }
}
