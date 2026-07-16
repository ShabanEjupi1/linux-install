using Microsoft.Data.Sqlite;

var dbPath = @"C:\Users\Dell\Desktop\POS\publish\production\Database\KosovaPOS.db";
using var connection = new SqliteConnection($"Data Source={dbPath}");
connection.Open();

var cmd = connection.CreateCommand();
cmd.CommandText = "SELECT COUNT(*) FROM Articles WHERE StockOut > 0";
var result = cmd.ExecuteScalar();
Console.WriteLine($"Articles with sales (StockOut > 0): {result}");

cmd.CommandText = "SELECT SUM(StockOut) FROM Articles";
result = cmd.ExecuteScalar();
Console.WriteLine($"Total items sold: {result}");
