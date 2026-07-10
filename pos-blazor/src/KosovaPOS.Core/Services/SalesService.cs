using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Models;
using KosovaPOS.Models.BMDData;

namespace KosovaPOS.Core.Services;

/// <summary>
/// Sale completion, ported from POS2/Services/SalesDataService.cs.
/// Posts each cart line to the BMD journal <see cref="DitariD"/> and decrements
/// stock on <see cref="Artikujt"/>, matching the desktop's accounting integration.
/// Static desktop methods became instance methods over an injected Npgsql factory,
/// and the write path is wrapped in a transaction for atomicity.
/// </summary>
public class SalesService
{
    private readonly IDbContextFactory<PosDbContext> _dbFactory;

    public SalesService(IDbContextFactory<PosDbContext> dbFactory) => _dbFactory = dbFactory;

    /// <summary>Next receipt/journal number = max(DitariD.Numri) + 1.</summary>
    public async Task<string> GetNextReceiptNumberAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var maxNumri = await db.DitariD.MaxAsync(d => (long?)d.Numri) ?? 0;
        return (maxNumri + 1).ToString();
    }

    /// <summary>
    /// Persists a complete receipt (journal rows + stock decrement) atomically.
    /// Returns the assigned journal number on success, or null on failure.
    /// </summary>
    public async Task<long?> SaveReceiptAsync(Receipt receipt)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var maxNumri = await db.DitariD.MaxAsync(d => (long?)d.Numri) ?? 0;
            var nextNumri = maxNumri + 1;
            int nrRendor = 1;

            foreach (var item in receipt.Items)
            {
                db.DitariD.Add(new DitariD
                {
                    Data = receipt.Date,
                    Ora = receipt.Date.ToString("HH:mm:ss"),
                    Numri = nextNumri,
                    Kuponi = receipt.ReceiptNumber,
                    Barkodi = item.Barcode,
                    Artikulli = item.ArticleName,
                    Njesia = "Copë",
                    Sasia = (double)item.Quantity,
                    Qmimi = (double)item.Price,
                    QmimiPaTvsh = (double)KosovoVat.NetUnitPrice(item.Price, item.VATRate),
                    QmimiF = null,
                    Rabati = (double)item.DiscountPercent,
                    VleraRabatit = (double)item.DiscountValue,
                    Vat = (double)item.VATRate,
                    Tvsh = (double)item.VATValue,
                    // Derived from the line VAT, not recomputed, so net + VAT == gross to the cent.
                    VleraPaTvsh = (double)(item.TotalValue - item.VATValue),
                    VleraMeTvsh = (double)item.TotalValue,
                    Punetori = receipt.CashierName ?? receipt.CashierNumber,
                    Viti = receipt.Date.Year.ToString(),
                    Muaji = receipt.Date.Month.ToString(),
                    MuajiNr = receipt.Date.Month,
                    Subjekti = 0,
                    NrFiskalKlient = null,
                    AdresaKlient = null,
                    ShifraKlient = receipt.BuyerName != "Qytetar" ? receipt.BuyerName : null,
                    Pagoi = (double)receipt.PaidAmount,
                    Mbeti = (double)receipt.LeftAmount,
                    MetodaP = GetPaymentMethodId(receipt.PaymentMethod),
                    ArtikullId = item.ArticleId,
                    NrRendor = nrRendor++,
                    Perpunimi = "POS"
                });

                var artikull = await db.Artikujt.FirstOrDefaultAsync(a => a.Id == item.ArticleId);
                if (artikull != null)
                {
                    artikull.Sasia = (artikull.Sasia ?? 0) - (double)item.Quantity;
                    artikull.SasiaDalje = (artikull.SasiaDalje ?? 0) + (double)item.Quantity;
                }
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return nextNumri;
        }
        catch
        {
            await tx.RollbackAsync();
            return null;
        }
    }

    /// <summary>Today's revenue, transaction count and items sold.</summary>
    public async Task<(decimal Revenue, int Transactions, int Items)> GetTodaySummaryAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var today = DateTime.Today;
        var rows = await db.DitariD.AsNoTracking()
            .Where(d => d.Data >= today && d.Data < today.AddDays(1))
            .Select(d => new { d.Numri, d.VleraMeTvsh })
            .ToListAsync();

        var revenue = (decimal)rows.Sum(r => r.VleraMeTvsh ?? 0);
        var transactions = rows.Select(r => r.Numri).Distinct().Count();
        return (revenue, transactions, rows.Count);
    }

    /// <summary>
    /// Recent receipts, newest first. Ported from SalesDataService.GetRecentSales,
    /// but the group-by aggregation runs in memory (Npgsql can't translate the
    /// desktop's <c>g.First()</c> projection inside GroupBy).
    /// </summary>
    public async Task<List<SalesSummary>> GetRecentSalesAsync(int count = 50)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var recentNumbers = await db.DitariD.AsNoTracking()
            .Select(d => d.Numri)
            .Distinct()
            .OrderByDescending(n => n)
            .Take(count)
            .ToListAsync();

        var rows = await db.DitariD.AsNoTracking()
            .Where(d => recentNumbers.Contains(d.Numri))
            .Select(d => new { d.Numri, d.Data, d.Ora, d.VleraMeTvsh, d.Punetori, d.MetodaP })
            .ToListAsync();

        return rows
            .GroupBy(d => d.Numri)
            .Select(g => new SalesSummary
            {
                ReceiptNumber = g.Key?.ToString() ?? "",
                Date = g.Max(d => d.Data) ?? DateTime.MinValue,
                Time = g.Select(d => d.Ora).FirstOrDefault(o => !string.IsNullOrEmpty(o)) ?? "",
                TotalAmount = (decimal)g.Sum(d => d.VleraMeTvsh ?? 0),
                ItemCount = g.Count(),
                Cashier = g.Select(d => d.Punetori).FirstOrDefault(p => !string.IsNullOrEmpty(p)) ?? "",
                PaymentMethod = PaymentMethodName(g.Select(d => d.MetodaP).FirstOrDefault() ?? 1)
            })
            .OrderByDescending(s => s.Date)
            .ToList();
    }

    /// <summary>All line items for a single receipt (journal number), in order.</summary>
    public async Task<List<ReceiptLine>> GetReceiptDetailAsync(long numri)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.DitariD.AsNoTracking()
            .Where(d => d.Numri == numri)
            .OrderBy(d => d.NrRendor)
            .Select(d => new ReceiptLine
            {
                Name = d.Artikulli ?? "",
                Barcode = d.Barkodi ?? "",
                Quantity = (decimal)(d.Sasia ?? 0),
                Price = (decimal)(d.Qmimi ?? 0),
                Total = (decimal)(d.VleraMeTvsh ?? 0),
                VATValue = (decimal)(d.Tvsh ?? 0)
            })
            .ToListAsync();
        return rows;
    }

    private static int GetPaymentMethodId(string method) => method?.ToLowerInvariant() switch
    {
        "para në dorë" or "cash" => 1,
        "kartë" or "card" => 2,
        "transfer" or "bankar" => 3,
        _ => 1
    };

    private static string PaymentMethodName(int id) => id switch
    {
        2 => "Kartë",
        3 => "Transfer",
        _ => "Para në dorë"
    };
}

/// <summary>One-row-per-receipt summary for the receipts list (ported from POS2 SalesSummary).</summary>
public class SalesSummary
{
    public string ReceiptNumber { get; set; } = "";
    public DateTime Date { get; set; }
    public string Time { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public string Cashier { get; set; } = "";
    public string PaymentMethod { get; set; } = "";
}

/// <summary>One journal line within a receipt, for the detail view.</summary>
public class ReceiptLine
{
    public string Name { get; set; } = "";
    public string Barcode { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Total { get; set; }
    public decimal VATValue { get; set; }
}
