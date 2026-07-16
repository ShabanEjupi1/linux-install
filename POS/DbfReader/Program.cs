using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DbfReader
{
    /// <summary>
    /// Utility to read dBase (DBF) files
    /// Reads the Articles.dbf file from the fiscal printer to see what article IDs are stored
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║          Kosovo POS - DBF File Reader                                ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            
            // Try multiple possible locations
            string[] possiblePaths = {
                @"C:\Program Files (x86)\ENTERNET\PGM-KS\Data\FP550_EN21003910\Articles.dbf",
                @"C:\Users\Administrator\POS\Articles.dbf",
                @"Articles.dbf"
            };
            
            string? dbfPath = null;
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    dbfPath = path;
                    break;
                }
            }
            
            if (dbfPath == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Articles.dbf file not found!");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("Searched locations:");
                foreach (var path in possiblePaths)
                {
                    Console.WriteLine($"  - {path}");
                }
                return;
            }
            
            Console.WriteLine($"✓ DBF file found: {dbfPath}");
            Console.WriteLine($"  Size: {new FileInfo(dbfPath).Length / 1024.0:F2} KB");
            Console.WriteLine();
            
            try
            {
                var records = ReadDbfFile(dbfPath);
                
                Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
                Console.WriteLine($"TOTAL RECORDS: {records.Count}");
                Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
                Console.WriteLine();
                
                if (records.Count > 0)
                {
                    Console.WriteLine("Sample records (first 20):");
                    Console.WriteLine();
                    
                    for (int i = 0; i < Math.Min(20, records.Count); i++)
                    {
                        Console.WriteLine($"Record {i + 1}:");
                        foreach (var field in records[i])
                        {
                            Console.WriteLine($"  {field.Key}: {field.Value}");
                        }
                        Console.WriteLine();
                    }
                    
                    // Show all article IDs
                    Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
                    Console.WriteLine("ALL ARTICLE IDs/NUMBERS:");
                    Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
                    Console.WriteLine();
                    
                    for (int i = 0; i < records.Count; i++)
                    {
                        var record = records[i];
                        
                        // Try to find ID/number field
                        string? id = null;
                        string? name = null;
                        
                        foreach (var field in record)
                        {
                            var key = field.Key.ToUpper();
                            if (key.Contains("ID") || key.Contains("NUM") || key.Contains("CODE") || key.Contains("PLU"))
                            {
                                id = field.Value;
                            }
                            if (key.Contains("NAME") || key.Contains("DESC") || key.Contains("EMERT"))
                            {
                                name = field.Value;
                            }
                        }
                        
                        Console.WriteLine($"{i + 1,4}. ID: {id ?? "N/A",-10} Name: {name ?? "N/A"}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Error reading DBF file: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
        
        static List<Dictionary<string, string>> ReadDbfFile(string filePath)
        {
            var records = new List<Dictionary<string, string>>();
            
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                // Read DBF header
                byte version = br.ReadByte();
                byte year = br.ReadByte();
                byte month = br.ReadByte();
                byte day = br.ReadByte();
                int recordCount = br.ReadInt32();
                short headerLength = br.ReadInt16();
                short recordLength = br.ReadInt16();
                
                Console.WriteLine("DBF File Information:");
                Console.WriteLine($"  Version: {version}");
                Console.WriteLine($"  Last Update: 20{year:D2}-{month:D2}-{day:D2}");
                Console.WriteLine($"  Record Count: {recordCount}");
                Console.WriteLine($"  Header Length: {headerLength}");
                Console.WriteLine($"  Record Length: {recordLength}");
                Console.WriteLine();
                
                // Skip reserved bytes
                br.ReadBytes(20);
                
                // Read field descriptors
                var fields = new List<DbfField>();
                
                while (true)
                {
                    byte fieldNameStart = br.ReadByte();
                    
                    // Check for end of field descriptors (0x0D)
                    if (fieldNameStart == 0x0D)
                    {
                        break;
                    }
                    
                    // Read field name (11 bytes total, first byte already read)
                    byte[] fieldNameBytes = new byte[11];
                    fieldNameBytes[0] = fieldNameStart;
                    br.Read(fieldNameBytes, 1, 10);
                    
                    string fieldName = Encoding.ASCII.GetString(fieldNameBytes).TrimEnd('\0');
                    
                    char fieldType = (char)br.ReadByte();
                    int fieldAddress = br.ReadInt32();
                    byte fieldLength = br.ReadByte();
                    byte decimalCount = br.ReadByte();
                    
                    // Skip reserved bytes
                    br.ReadBytes(14);
                    
                    fields.Add(new DbfField
                    {
                        Name = fieldName,
                        Type = fieldType,
                        Length = fieldLength,
                        DecimalCount = decimalCount
                    });
                }
                
                Console.WriteLine("Field Structure:");
                foreach (var field in fields)
                {
                    Console.WriteLine($"  {field.Name,-15} Type: {field.Type}  Length: {field.Length,3}  Decimals: {field.DecimalCount}");
                }
                Console.WriteLine();
                
                // Position to start of records
                fs.Seek(headerLength, SeekOrigin.Begin);
                
                // Read all records
                for (int i = 0; i < recordCount; i++)
                {
                    var record = new Dictionary<string, string>();
                    
                    // Read deletion flag
                    byte deletionFlag = br.ReadByte();
                    
                    // Skip deleted records
                    if (deletionFlag == 0x2A) // '*' means deleted
                    {
                        br.ReadBytes(recordLength - 1);
                        continue;
                    }
                    
                    // Read each field
                    foreach (var field in fields)
                    {
                        byte[] fieldData = br.ReadBytes(field.Length);
                        string value = Encoding.ASCII.GetString(fieldData).Trim();
                        
                        record[field.Name] = value;
                    }
                    
                    records.Add(record);
                }
            }
            
            return records;
        }
        
        class DbfField
        {
            public string Name { get; set; } = "";
            public char Type { get; set; }
            public byte Length { get; set; }
            public byte DecimalCount { get; set; }
        }
    }
}
