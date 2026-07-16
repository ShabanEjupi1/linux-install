using System;
using System.Collections.Generic;
using System.Linq;
using KosovaPOS.Database;
using KosovaPOS.Models;
using KosovaPOS.Models.BMDData;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS.Services
{
    /// <summary>
    /// Data access service for sales operations via SQL Server (DitariD/DitariH)
    /// </summary>
    public static class SalesDataService
    {
        /// <summary>
        /// Save a complete receipt with all items
        /// </summary>
        public static bool SaveReceipt(Receipt receipt)
        {
            try
            {
                using var context = new POSDbContext();
                
                // First, create the header record (DitariH)
                var ditariH = new DitariH
                {
                    Data = receipt.Date,
                    KlientiID = null, // Default customer
                    Klienti = receipt.BuyerName != "Qytetar" ? receipt.BuyerName : "Klient i përgjithshëm",
                    ArkaID = 1, // Default cash register
                    Arka = "Arka 1",
                    Shuma = (double)receipt.Items.Sum(i => i.TotalValue - i.VATValue),
                    Zbritja = (double)receipt.Items.Sum(i => i.DiscountValue),
                    Tatimi = (double)receipt.Items.Sum(i => i.VATValue),
                    Totali = (double)receipt.TotalAmount,
                    Paguar = (double)receipt.PaidAmount,
                    Mbetur = (double)receipt.LeftAmount,
                    PunetoriID = null,
                    Punetori = receipt.CashierName ?? receipt.CashierNumber,
                    Verejtje = receipt.Remark,
                    Filiala = 1,
                    TipiPageses = receipt.PaymentMethod
                };
                
                context.DitariH.Add(ditariH);
                context.SaveChanges(); // Save to get the generated ID
                
                var ditariHId = ditariH.Id;
                
                // Now create detail records for each item
                foreach (var item in receipt.Items)
                {
                    // Create detail record using actual schema fields
                    var ditariD = new DitariD
                    {
                        DitariHID = ditariHId, // Link to header
                        ArtikujID = item.ArticleId,
                        Barkodi = item.Barcode,
                        Emertimi = item.ArticleName,
                        Sasia = (double)item.Quantity,
                        Cmimi = (double)item.Price,
                        Zbritja = (double)item.DiscountValue,
                        Tatimi = (double)item.VATValue,
                        Totali = (double)item.TotalValue,
                        Vat = (double)item.VATRate
                    };
                    
                    context.DitariD.Add(ditariD);
                    
                    // Update stock
                    var artikull = context.Artikujt.FirstOrDefault(a => a.Id == item.ArticleId);
                    if (artikull != null)
                    {
                        artikull.Sasia = (artikull.Sasia ?? 0) - (double)item.Quantity;
                        artikull.SasiaDalje = (artikull.SasiaDalje ?? 0) + (double)item.Quantity;
                    }
                }
                
                context.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveReceipt error: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Get today's sales summary
        /// </summary>
        public static (decimal TotalRevenue, int TransactionCount, int ItemsSold) GetTodaySummary()
        {
            using var context = new POSDbContext();
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // Filter by DitariH.Data (mapped column), then retrieve DitariD by DitariHID
            var todayHeaderIds = context.DitariH
                .Where(h => h.Data.HasValue && h.Data >= today && h.Data < tomorrow)
                .Select(h => h.Id)
                .ToList();

            if (!todayHeaderIds.Any())
                return (0, 0, 0);

            var todaySales = context.DitariD
                .Where(d => d.DitariHID.HasValue && todayHeaderIds.Contains(d.DitariHID.Value))
                .ToList();

            var revenue = (decimal)todaySales.Sum(d => d.Totali ?? 0);
            var transactionCount = todaySales.Select(d => d.DitariHID).Distinct().Count();
            var itemsSold = todaySales.Count;

            return (revenue, transactionCount, itemsSold);
        }

        /// <summary>
        /// Get sales for a date range
        /// </summary>
        public static List<SalesSummary> GetSalesForDateRange(DateTime startDate, DateTime endDate)
        {
            using var context = new POSDbContext();

            // Use DitariH for date filtering (Data is a mapped column on DitariH)
            var headers = context.DitariH
                .Where(h => h.Data.HasValue && h.Data >= startDate && h.Data <= endDate)
                .OrderByDescending(h => h.Data)
                .ToList();

            if (!headers.Any())
                return new List<SalesSummary>();

            var headerIds = headers.Select(h => h.Id).ToList();
            var itemCountByHeader = context.DitariD
                .Where(d => d.DitariHID.HasValue && headerIds.Contains(d.DitariHID.Value))
                .GroupBy(d => d.DitariHID!.Value)
                .Select(g => new { HeaderId = g.Key, Count = g.Count() })
                .ToDictionary(x => x.HeaderId, x => x.Count);

            return headers.Select(h => new SalesSummary
            {
                ReceiptNumber = h.Id.ToString(),
                Date = h.Data ?? DateTime.MinValue,
                TotalAmount = (decimal)(h.Totali ?? 0),
                ItemCount = itemCountByHeader.TryGetValue(h.Id, out var cnt) ? cnt : 0,
                Cashier = h.Punetori ?? ""
            }).ToList();
        }

        /// <summary>
        /// Get recent sales (last N transactions)
        /// </summary>
        public static List<SalesSummary> GetRecentSales(int count = 50)
        {
            using var context = new POSDbContext();

            var recentHeaders = context.DitariH
                .OrderByDescending(h => h.Id)
                .Take(count)
                .ToList();

            if (!recentHeaders.Any())
                return new List<SalesSummary>();

            var headerIds = recentHeaders.Select(h => h.Id).ToList();
            var itemCountByHeader = context.DitariD
                .Where(d => d.DitariHID.HasValue && headerIds.Contains(d.DitariHID.Value))
                .GroupBy(d => d.DitariHID!.Value)
                .Select(g => new { HeaderId = g.Key, Count = g.Count() })
                .ToDictionary(x => x.HeaderId, x => x.Count);

            return recentHeaders.Select(h => new SalesSummary
            {
                ReceiptNumber = h.Id.ToString(),
                Date = h.Data ?? DateTime.MinValue,
                TotalAmount = (decimal)(h.Totali ?? 0),
                ItemCount = itemCountByHeader.TryGetValue(h.Id, out var cnt) ? cnt : 0,
                Cashier = h.Punetori ?? ""
            })
            .OrderByDescending(s => s.Date)
            .ToList();
        }

        /// <summary>
        /// Get next receipt number
        /// </summary>
        public static string GetNextReceiptNumber()
        {
            using var context = new POSDbContext();

            var maxId = context.DitariH.Max(h => (long?)h.Id) ?? 0;
            return (maxId + 1).ToString();
        }
        
        private static int GetPaymentMethodId(string method)
        {
            return method?.ToLower() switch
            {
                "para në dorë" or "cash" => 1,
                "kartë" or "card" => 2,
                "transfer" or "bankar" => 3,
                _ => 1
            };
        }
    }
    
    /// <summary>
    /// Summary class for sales reports
    /// </summary>
    public class SalesSummary
    {
        public string ReceiptNumber { get; set; } = "";
        public DateTime Date { get; set; }
        public decimal TotalAmount { get; set; }
        public int ItemCount { get; set; }
        public string Cashier { get; set; } = "";
    }
}
