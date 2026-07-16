using System;
using System.IO;
using Microsoft.Data.Sqlite;

string dbPath = @"c:\Users\Dell\Desktop\POS\publish\production\Database\KosovaPOS.db";

if (!File.Exists(dbPath))
{
    Console.WriteLine($"Database not found at: {dbPath}");
    return;
}

Console.WriteLine($"Database found: {dbPath}");
Console.WriteLine($"Size: {new FileInfo(dbPath).Length:N0} bytes");
Console.WriteLine();

using var connection = new SqliteConnection($"Data Source={dbPath}");
connection.Open();

// List all tables
Console.WriteLine("=== TABLES IN DATABASE ===");
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        Console.WriteLine($"  - {reader.GetString(0)}");
    }
}
Console.WriteLine();

// Get record counts for each table
Console.WriteLine("=== RECORD COUNTS ===");
string[] tables = { "Articles", "Receipts", "ReceiptItems", "BusinessPartners", "Purchases", "PurchaseItems", "Users", "AuditLogs", "__EFMigrationsHistory" };

foreach (var table in tables)
{
    try
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
        var count = cmd.ExecuteScalar();
        Console.WriteLine($"  {table}: {count:N0} records");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  {table}: Table not found or error - {ex.Message}");
    }
}
Console.WriteLine();

// Sample Articles - show structure
Console.WriteLine("=== ARTICLES TABLE STRUCTURE ===");
try
{
    using var cmd = connection.CreateCommand();
    cmd.CommandText = "PRAGMA table_info(Articles)";
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        Console.WriteLine($"  Column: {reader["name"]} | Type: {reader["type"]} | NotNull: {reader["notnull"]} | Default: {reader["dflt_value"]}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
Console.WriteLine();

// Sample Articles data
Console.WriteLine("=== SAMPLE ARTICLES (first 10) ===");
try
{
    using var cmd = connection.CreateCommand();
    cmd.CommandText = "SELECT Id, Name, Barcode, PLU, SalesPrice, StockQuantity FROM Articles LIMIT 10";
    using var reader = cmd.ExecuteReader();
    
    while (reader.Read())
    {
        Console.WriteLine($"  ID: {reader["Id"]}, PLU: {(reader["PLU"] == DBNull.Value ? "NULL" : reader["PLU"])}, Price: {reader["SalesPrice"]}, Stock: {reader["StockQuantity"]}, Barcode: {reader["Barcode"]}, Name: {reader["Name"]}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error reading Articles: {ex.Message}");
}
Console.WriteLine();

// Check for PLU data
Console.WriteLine("=== PLU DATA ANALYSIS ===");
try
{
    using var cmd = connection.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM Articles WHERE PLU IS NOT NULL AND PLU > 0";
    var pluCount = cmd.ExecuteScalar();
    Console.WriteLine($"  Articles with PLU assigned: {pluCount}");
    
    cmd.CommandText = "SELECT COUNT(*) FROM Articles WHERE PLU IS NULL OR PLU = 0";
    var noPluCount = cmd.ExecuteScalar();
    Console.WriteLine($"  Articles without PLU: {noPluCount}");
    
    cmd.CommandText = "SELECT COUNT(*) FROM Articles";
    var totalCount = cmd.ExecuteScalar();
    Console.WriteLine($"  Total Articles: {totalCount}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error checking PLU: {ex.Message}");
}
Console.WriteLine();

// Check Users
Console.WriteLine("=== USERS ===");
try
{
    using var cmd = connection.CreateCommand();
    cmd.CommandText = "SELECT Id, Username, Role FROM Users";
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        Console.WriteLine($"  ID: {reader["Id"]}, Username: {reader["Username"]}, Role: {reader["Role"]}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error reading Users: {ex.Message}");
}
Console.WriteLine();

// Check BusinessPartners structure
Console.WriteLine("=== BUSINESS PARTNERS TABLE STRUCTURE ===");
try
{
    using var cmd = connection.CreateCommand();
    cmd.CommandText = "PRAGMA table_info(BusinessPartners)";
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        Console.WriteLine($"  Column: {reader["name"]} | Type: {reader["type"]}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
Console.WriteLine();

// Check Receipts table structure
Console.WriteLine("=== RECEIPTS TABLE STRUCTURE ===");
try
{
    using var cmd = connection.CreateCommand();
    cmd.CommandText = "PRAGMA table_info(Receipts)";
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        Console.WriteLine($"  Column: {reader["name"]} | Type: {reader["type"]}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
Console.WriteLine();

// Check Purchases table structure
Console.WriteLine("=== PURCHASES TABLE STRUCTURE ===");
try
{
    using var cmd = connection.CreateCommand();
    cmd.CommandText = "PRAGMA table_info(Purchases)";
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        Console.WriteLine($"  Column: {reader["name"]} | Type: {reader["type"]}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
Console.WriteLine();

// Sample BusinessPartners
Console.WriteLine("=== SAMPLE BUSINESS PARTNERS (first 10) ===");
try
{
    using var cmd = connection.CreateCommand();
    cmd.CommandText = "SELECT Id, Name, PartnerType, Email, Phone FROM BusinessPartners LIMIT 10";
    using var reader = cmd.ExecuteReader();
    
    while (reader.Read())
    {
        Console.WriteLine($"  ID: {reader["Id"]}, Type: {reader["PartnerType"]}, Name: {reader["Name"]}, Email: {reader["Email"]}, Phone: {reader["Phone"]}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error reading BusinessPartners: {ex.Message}");
}
Console.WriteLine();

// Sample Receipts - use actual column names
Console.WriteLine("=== RECENT RECEIPTS (last 10) ===");
try
{
    using var cmd = connection.CreateCommand();
    cmd.CommandText = "SELECT * FROM Receipts ORDER BY Id DESC LIMIT 1";
    using var reader = cmd.ExecuteReader();
    
    // First, get the column names
    var columns = new List<string>();
    for (int i = 0; i < reader.FieldCount; i++)
    {
        columns.Add(reader.GetName(i));
    }
    Console.WriteLine($"  Columns: {string.Join(", ", columns)}");
    
    while (reader.Read())
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var value = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i)?.ToString();
            Console.WriteLine($"    {columns[i]}: {value}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error reading Receipts: {ex.Message}");
}
Console.WriteLine();

// Receipts summary
Console.WriteLine("=== RECEIPTS SUMMARY ===");
try
{
    using var cmd = connection.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM Receipts";
    Console.WriteLine($"  Total Receipts: {cmd.ExecuteScalar()}");
    
    cmd.CommandText = "SELECT SUM(TotalAmount) FROM Receipts";
    Console.WriteLine($"  Total Revenue: {cmd.ExecuteScalar()}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
Console.WriteLine();

// Check Purchases - use actual columns
Console.WriteLine("=== PURCHASES SUMMARY ===");
try
{
    using var cmd = connection.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM Purchases";
    var purchaseCount = cmd.ExecuteScalar();
    Console.WriteLine($"  Total Purchases: {purchaseCount}");
    
    // Get a sample purchase to see column names
    cmd.CommandText = "SELECT * FROM Purchases LIMIT 1";
    using var reader = cmd.ExecuteReader();
    var columns = new List<string>();
    for (int i = 0; i < reader.FieldCount; i++)
    {
        columns.Add(reader.GetName(i));
    }
    Console.WriteLine($"  Columns: {string.Join(", ", columns)}");
    
    while (reader.Read())
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var value = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i)?.ToString();
            Console.WriteLine($"    {columns[i]}: {value}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error reading Purchases: {ex.Message}");
}
Console.WriteLine();

// Check ReceiptItems
Console.WriteLine("=== RECEIPT ITEMS SUMMARY ===");
try
{
    using var cmd = connection.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM ReceiptItems";
    Console.WriteLine($"  Total ReceiptItems: {cmd.ExecuteScalar()}");
    
    cmd.CommandText = "SELECT COUNT(DISTINCT ArticleId) FROM ReceiptItems";
    Console.WriteLine($"  Unique Articles Sold: {cmd.ExecuteScalar()}");
    
    cmd.CommandText = "SELECT SUM(Quantity) FROM ReceiptItems";
    Console.WriteLine($"  Total Items Sold: {cmd.ExecuteScalar()}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
Console.WriteLine();

// Sample ReceiptItems
Console.WriteLine("=== SAMPLE RECEIPT ITEMS (last 10) ===");
try
{
    using var cmd = connection.CreateCommand();
    cmd.CommandText = @"SELECT ri.Id, ri.ReceiptId, ri.ArticleId, ri.Quantity, ri.Price, ri.PLU, a.Name as ArticleName 
                        FROM ReceiptItems ri 
                        LEFT JOIN Articles a ON ri.ArticleId = a.Id 
                        ORDER BY ri.Id DESC LIMIT 10";
    using var reader = cmd.ExecuteReader();
    
    while (reader.Read())
    {
        Console.WriteLine($"  ID: {reader["Id"]}, Receipt: {reader["ReceiptId"]}, Article: {reader["ArticleId"]}, PLU: {(reader["PLU"] == DBNull.Value ? "NULL" : reader["PLU"])}, Qty: {reader["Quantity"]}, Price: {reader["Price"]}, Name: {reader["ArticleName"]}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
Console.WriteLine();

// Check AuditLogs
Console.WriteLine("=== AUDIT LOGS SUMMARY ===");
try
{
    using var cmd = connection.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM AuditLogs";
    Console.WriteLine($"  Total AuditLogs: {cmd.ExecuteScalar()}");
    
    cmd.CommandText = "SELECT Action, COUNT(*) as Count FROM AuditLogs GROUP BY Action ORDER BY Count DESC LIMIT 10";
    using var reader = cmd.ExecuteReader();
    Console.WriteLine("  Actions breakdown:");
    while (reader.Read())
    {
        Console.WriteLine($"    {reader["Action"]}: {reader["Count"]}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

Console.WriteLine("\n=== DONE ===");
