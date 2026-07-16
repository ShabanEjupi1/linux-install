using Microsoft.Data.Sqlite;
using System;
using System.IO;

// Check stock quantities in the database
var dbPath = @"C:\Users\Dell\Desktop\POS\publish\production\Database\KosovaPOS.db";

Console.WriteLine($"Checking database: {dbPath}");
Console.WriteLine();

if (!File.Exists(dbPath))
{
    Console.WriteLine("Database not found!");
    return;
}

using var connection = new SqliteConnection($"Data Source={dbPath}");
connection.Open();

// Check stock statistics
var cmd = connection.CreateCommand();
cmd.CommandText = @"
    SELECT 
        COUNT(*) as TotalArticles,
        SUM(CASE WHEN StockQuantity > 0 THEN 1 ELSE 0 END) as WithStock,
        SUM(CASE WHEN StockQuantity = 0 THEN 1 ELSE 0 END) as ZeroStock,
        SUM(CASE WHEN StockQuantity < 0 THEN 1 ELSE 0 END) as NegativeStock,
        COALESCE(AVG(StockQuantity), 0) as AvgStock,
        COALESCE(SUM(StockQuantity), 0) as TotalStock
    FROM Articles WHERE IsActive = 1";

using var reader = cmd.ExecuteReader();
if (reader.Read())
{
    Console.WriteLine("Stock Statistics:");
    Console.WriteLine("==================");
    Console.WriteLine($"Total Active Articles: {reader.GetInt32(0)}");
    Console.WriteLine($"With Stock (>0): {reader.GetInt64(1)}");
    Console.WriteLine($"Zero Stock (=0): {reader.GetInt64(2)}");
    Console.WriteLine($"Negative Stock (<0): {reader.GetInt64(3)}");
    Console.WriteLine($"Average Stock: {reader.GetDouble(4):F2}");
    Console.WriteLine($"Total Stock: {reader.GetDouble(5):F2}");
}
reader.Close();

// Show articles with various stock levels
Console.WriteLine();
Console.WriteLine("Articles with Positive Stock:");
Console.WriteLine("==============================");

cmd.CommandText = "SELECT Id, Name, StockQuantity, StockIn, StockOut FROM Articles WHERE IsActive = 1 AND StockQuantity > 0 ORDER BY StockQuantity DESC LIMIT 10";
using var reader2 = cmd.ExecuteReader();
while (reader2.Read())
{
    var id = reader2.GetInt32(0);
    var name = reader2.GetString(1);
    if (name.Length > 30) name = name.Substring(0, 27) + "...";
    var stock = reader2.GetDouble(2);
    var stockIn = reader2.GetDouble(3);
    var stockOut = reader2.GetDouble(4);
    Console.WriteLine($"  [{id}] {name,-30} Stock: {stock,8:F2}  In: {stockIn,8:F2}  Out: {stockOut,8:F2}");
}
reader2.Close();

Console.WriteLine();
Console.WriteLine("Articles with Negative Stock:");
Console.WriteLine("==============================");

cmd.CommandText = "SELECT Id, Name, StockQuantity, StockIn, StockOut FROM Articles WHERE IsActive = 1 AND StockQuantity < 0 ORDER BY StockQuantity LIMIT 10";
using var reader3 = cmd.ExecuteReader();
while (reader3.Read())
{
    var id = reader3.GetInt32(0);
    var name = reader3.GetString(1);
    if (name.Length > 30) name = name.Substring(0, 27) + "...";
    var stock = reader3.GetDouble(2);
    var stockIn = reader3.GetDouble(3);
    var stockOut = reader3.GetDouble(4);
    Console.WriteLine($"  [{id}] {name,-30} Stock: {stock,8:F2}  In: {stockIn,8:F2}  Out: {stockOut,8:F2}");
}
reader3.Close();

Console.WriteLine();
Console.WriteLine("Random sample from Zero Stock:");
Console.WriteLine("===============================");

cmd.CommandText = "SELECT Id, Name, StockQuantity, StockIn, StockOut FROM Articles WHERE IsActive = 1 AND StockQuantity = 0 ORDER BY RANDOM() LIMIT 10";
using var reader4 = cmd.ExecuteReader();
while (reader4.Read())
{
    var id = reader4.GetInt32(0);
    var name = reader4.GetString(1);
    if (name.Length > 30) name = name.Substring(0, 27) + "...";
    var stock = reader4.GetDouble(2);
    var stockIn = reader4.GetDouble(3);
    var stockOut = reader4.GetDouble(4);
    Console.WriteLine($"  [{id}] {name,-30} Stock: {stock,8:F2}  In: {stockIn,8:F2}  Out: {stockOut,8:F2}");
}
