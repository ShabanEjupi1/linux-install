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

    /// <summary>
    /// A full invoice for one receipt (journal number): header + priced lines +
    /// a VAT-by-rate summary, everything the A4 legal invoice (faturë) needs.
    /// Returns null when the number matches no journal rows.
    /// </summary>
    public async Task<Invoice?> GetInvoiceAsync(long numri)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.DitariD.AsNoTracking()
            .Where(d => d.Numri == numri)
            .OrderBy(d => d.NrRendor)
            .ToListAsync();
        if (rows.Count == 0)
            return null;

        var head = rows[0];
        var lines = rows.Select(d => new InvoiceLine
        {
            Name = d.Artikulli ?? "",
            Unit = string.IsNullOrWhiteSpace(d.Njesia) ? "Copë" : d.Njesia!,
            Quantity = (decimal)(d.Sasia ?? 0),
            UnitPriceGross = (decimal)(d.Qmimi ?? 0),
            UnitPriceNet = (decimal)(d.QmimiPaTvsh ?? 0),
            VatRate = (decimal)(d.Vat ?? 0),
            DiscountPercent = (decimal)(d.Rabati ?? 0),
            NetValue = (decimal)(d.VleraPaTvsh ?? 0),
            VatValue = (decimal)(d.Tvsh ?? 0),
            GrossValue = (decimal)(d.VleraMeTvsh ?? 0),
        }).ToList();

        var vatSummary = lines
            .GroupBy(l => l.VatRate)
            .Select(g => new VatSummaryRow
            {
                Rate = g.Key,
                Net = g.Sum(l => l.NetValue),
                Vat = g.Sum(l => l.VatValue),
                Gross = g.Sum(l => l.GrossValue),
            })
            .OrderByDescending(r => r.Rate)
            .ToList();

        return new Invoice
        {
            Number = numri.ToString(),
            CouponNumber = head.Kuponi,
            Date = head.Data ?? DateTime.MinValue,
            Time = head.Ora ?? "",
            CashierName = head.Punetori ?? "",
            PaymentMethod = PaymentMethodName(head.MetodaP ?? 1),
            BuyerName = head.ShifraKlient,
            BuyerFiscalNumber = head.NrFiskalKlient,
            BuyerAddress = head.AdresaKlient,
            Lines = lines,
            VatSummary = vatSummary,
            TotalNet = lines.Sum(l => l.NetValue),
            TotalVat = lines.Sum(l => l.VatValue),
            TotalGross = lines.Sum(l => l.GrossValue),
            // Written identically onto every line of the sale, so the head carries them.
            PaidAmount = (decimal)(head.Pagoi ?? 0),
            ChangeAmount = (decimal)(head.Mbeti ?? 0),
        };
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

/// <summary>A full priced invoice for one receipt, for the A4 legal document.</summary>
public class Invoice
{
    public string Number { get; set; } = "";
    public string? CouponNumber { get; set; }
    public DateTime Date { get; set; }
    public string Time { get; set; } = "";
    public string CashierName { get; set; } = "";
    public string PaymentMethod { get; set; } = "";

    // Buyer as recorded on the sale (may be blank for a walk-in "Qytetar").
    public string? BuyerName { get; set; }
    public string? BuyerFiscalNumber { get; set; }
    public string? BuyerAddress { get; set; }

    public List<InvoiceLine> Lines { get; set; } = new();
    public List<VatSummaryRow> VatSummary { get; set; } = new();
    public decimal TotalNet { get; set; }
    public decimal TotalVat { get; set; }
    public decimal TotalGross { get; set; }

    /// <summary>Cash the customer handed over. Equal to the total on a card sale.</summary>
    public decimal PaidAmount { get; set; }

    /// <summary>Change handed back. On DitariD this is what <c>Mbeti</c> holds — NOT an amount still owed.</summary>
    public decimal ChangeAmount { get; set; }
}

public class InvoiceLine
{
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "Copë";
    public decimal Quantity { get; set; }
    public decimal UnitPriceGross { get; set; }
    public decimal UnitPriceNet { get; set; }
    public decimal VatRate { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal NetValue { get; set; }
    public decimal VatValue { get; set; }
    public decimal GrossValue { get; set; }
}

public class VatSummaryRow
{
    public decimal Rate { get; set; }
    public decimal Net { get; set; }
    public decimal Vat { get; set; }
    public decimal Gross { get; set; }
}
