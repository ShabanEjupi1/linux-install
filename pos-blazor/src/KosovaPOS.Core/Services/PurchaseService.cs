using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Models.BMDData;

namespace KosovaPOS.Core.Services;

/// <summary>
/// Purchase (blerje) entry, ported from POS2/Windows/PurchaseEditWindow.xaml.cs.
/// The mirror of <see cref="SalesService"/>: posts each line to the BMD purchase
/// journal <see cref="DitariH"/> and <b>increments</b> stock on <see cref="Artikujt"/>
/// (plus refreshes purchase/sales price), all in a transaction. Suppliers come from
/// <see cref="FurnitoriNew"/> (F = is-supplier), matching the desktop combo box.
/// </summary>
public class PurchaseService
{
    private readonly IDbContextFactory<PosDbContext> _dbFactory;

    public PurchaseService(IDbContextFactory<PosDbContext> dbFactory) => _dbFactory = dbFactory;

    /// <summary>Suppliers for the entry dropdown (F = is-supplier; falls back to all).</summary>
    public async Task<List<Supplier>> GetSuppliersAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var q = db.FurnitoriNew.AsNoTracking();
        // Prefer rows explicitly flagged as suppliers; if none are flagged, show all.
        var anyFlagged = await q.AnyAsync(f => f.F == true);
        if (anyFlagged) q = q.Where(f => f.F == true);
        var rows = await q
            .OrderBy(f => f.Emri)
            .Select(f => new Supplier { Id = (int)f.Id, Name = f.Emri ?? $"Furnitor {f.Id}" })
            .ToListAsync();
        return rows;
    }

    /// <summary>Next purchase document number = max(DitariH.Numri) + 1.</summary>
    public async Task<string> GetNextDocumentNumberAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var max = await db.DitariH.MaxAsync(d => (long?)d.Numri) ?? 0;
        return (max + 1).ToString();
    }

    /// <summary>
    /// Persists a purchase (journal rows + stock/price update) atomically.
    /// Returns the assigned journal number on success, or null on failure.
    /// </summary>
    public async Task<long?> SavePurchaseAsync(PurchaseDraft draft)
    {
        if (draft.Items.Count == 0) return null;

        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var maxNumri = await db.DitariH.MaxAsync(d => (long?)d.Numri) ?? 0;
            var nextNumri = maxNumri + 1;
            int nrRendor = 1;

            foreach (var item in draft.Items)
            {
                var priceAfterRabat = item.PurchasePrice * (1 - item.DiscountPercent / 100);
                var vatValue = priceAfterRabat * item.Quantity * (item.VATRate / 100);
                var totalWithVat = priceAfterRabat * item.Quantity + vatValue;

                db.DitariH.Add(new DitariH
                {
                    Data = draft.Date,
                    Ora = draft.Date.ToString("HH:mm:ss"),
                    Numri = nextNumri,
                    NrFatures = draft.DocumentNumber,
                    NrDUD = draft.DocumentNumber,
                    Subjekti = draft.SupplierId > 0 ? draft.SupplierId : null,
                    Tipi = draft.PurchaseType,
                    Punetori = draft.Worker,
                    Barkodi = item.Barcode,
                    ArtikullId = item.ArticleId,
                    Artikulli = item.ArticleName,
                    Njesia = "Copë",
                    Sasia = (double)item.Quantity,
                    CmimiFurn = (double)item.PurchasePrice,
                    RabatiPer = (double)item.DiscountPercent,
                    RabatiVl = (double)(item.PurchasePrice * item.Quantity * item.DiscountPercent / 100),
                    VleraFurn = (double)(priceAfterRabat * item.Quantity),
                    TvshPer = (double)item.VATRate,
                    TvshVl = (double)vatValue,
                    VleraMeTvsh = (double)totalWithVat,
                    CmShitjes = (double)item.SalesPrice,
                    PerPagese = (double)totalWithVat,
                    Pagoi = draft.IsPaid ? (double)totalWithVat : 0,
                    Mbeti = draft.IsPaid ? 0 : (double)totalWithVat,
                    NrRendor = nrRendor++
                });

                var artikull = await db.Artikujt.FirstOrDefaultAsync(a => a.Id == item.ArticleId);
                if (artikull != null)
                {
                    artikull.Sasia = (artikull.Sasia ?? 0) + (double)item.Quantity;
                    artikull.SasiaHyrje = (artikull.SasiaHyrje ?? 0) + (double)item.Quantity;
                    artikull.CFurnizimit = (double)item.PurchasePrice;
                    if (item.SalesPrice > 0)
                        artikull.CShitjes = (double)item.SalesPrice;
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

    /// <summary>Recent purchase documents, newest first (mirror of GetRecentSales).</summary>
    public async Task<List<PurchaseSummary>> GetRecentPurchasesAsync(int count = 50)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var recentNumbers = await db.DitariH.AsNoTracking()
            .Select(d => d.Numri)
            .Distinct()
            .OrderByDescending(n => n)
            .Take(count)
            .ToListAsync();

        var rows = await db.DitariH.AsNoTracking()
            .Where(d => recentNumbers.Contains(d.Numri))
            .Select(d => new { d.Numri, d.Data, d.Ora, d.NrFatures, d.Tipi, d.VleraMeTvsh })
            .ToListAsync();

        return rows
            .GroupBy(d => d.Numri)
            .Select(g => new PurchaseSummary
            {
                DocumentNumber = g.Key?.ToString() ?? "",
                InvoiceNumber = g.Select(d => d.NrFatures).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "",
                Date = g.Max(d => d.Data) ?? DateTime.MinValue,
                Time = g.Select(d => d.Ora).FirstOrDefault(o => !string.IsNullOrEmpty(o)) ?? "",
                PurchaseType = g.Select(d => d.Tipi).FirstOrDefault(t => !string.IsNullOrEmpty(t)) ?? "",
                ItemCount = g.Count(),
                TotalAmount = (decimal)g.Sum(d => d.VleraMeTvsh ?? 0)
            })
            .OrderByDescending(s => s.Date)
            .ToList();
    }

    /// <summary>
    /// Deletes a purchase document and reverses exactly the stock it added.
    ///
    /// The reversal subtracts each line's own recorded quantity rather than
    /// recomputing anything, so a correction undoes precisely what the original
    /// entry did. Stock is allowed to go negative here: the goods may already have
    /// been sold, and refusing would make a mistaken purchase impossible to correct
    /// — the honest outcome is a visible negative on /stoku, not a fabricated one.
    /// Article prices (CFurnizimit/CShitjes) are NOT rolled back: the later price is
    /// a business fact, and no earlier value is recorded to restore.
    /// </summary>
    public async Task<bool> DeletePurchaseAsync(long numri)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var rows = await db.DitariH.Where(d => d.Numri == numri).ToListAsync();
            if (rows.Count == 0) return false;

            await ReverseStockAsync(db, rows);
            db.DitariH.RemoveRange(rows);

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            return false;
        }
    }

    /// <summary>
    /// Rewrites an existing purchase document in place: reverses the stock the old
    /// lines added, replaces them with the draft's lines under the same document
    /// number, and applies the new stock. One transaction, so a failure leaves the
    /// original untouched.
    /// </summary>
    public async Task<bool> UpdatePurchaseAsync(long numri, PurchaseDraft draft)
    {
        if (draft.Items.Count == 0) return false;

        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var old = await db.DitariH.Where(d => d.Numri == numri).ToListAsync();
            if (old.Count == 0) return false;

            await ReverseStockAsync(db, old);
            db.DitariH.RemoveRange(old);
            await db.SaveChangesAsync();   // flush the delete before re-inserting the same Numri

            int nrRendor = 1;
            foreach (var item in draft.Items)
            {
                var priceAfterRabat = item.PurchasePrice * (1 - item.DiscountPercent / 100);
                var vatValue = priceAfterRabat * item.Quantity * (item.VATRate / 100);
                var totalWithVat = priceAfterRabat * item.Quantity + vatValue;

                db.DitariH.Add(new DitariH
                {
                    Data = draft.Date,
                    Ora = draft.Date.ToString("HH:mm:ss"),
                    Numri = numri,
                    NrFatures = draft.DocumentNumber,
                    NrDUD = draft.DocumentNumber,
                    Subjekti = draft.SupplierId > 0 ? draft.SupplierId : null,
                    Tipi = draft.PurchaseType,
                    Punetori = draft.Worker,
                    Barkodi = item.Barcode,
                    ArtikullId = item.ArticleId,
                    Artikulli = item.ArticleName,
                    Njesia = "Copë",
                    Sasia = (double)item.Quantity,
                    CmimiFurn = (double)item.PurchasePrice,
                    RabatiPer = (double)item.DiscountPercent,
                    RabatiVl = (double)(item.PurchasePrice * item.Quantity * item.DiscountPercent / 100),
                    VleraFurn = (double)(priceAfterRabat * item.Quantity),
                    TvshPer = (double)item.VATRate,
                    TvshVl = (double)vatValue,
                    VleraMeTvsh = (double)totalWithVat,
                    CmShitjes = (double)item.SalesPrice,
                    PerPagese = (double)totalWithVat,
                    Pagoi = draft.IsPaid ? (double)totalWithVat : 0,
                    Mbeti = draft.IsPaid ? 0 : (double)totalWithVat,
                    NrRendor = nrRendor++
                });

                var artikull = await db.Artikujt.FirstOrDefaultAsync(a => a.Id == item.ArticleId);
                if (artikull != null)
                {
                    artikull.Sasia = (artikull.Sasia ?? 0) + (double)item.Quantity;
                    artikull.SasiaHyrje = (artikull.SasiaHyrje ?? 0) + (double)item.Quantity;
                    artikull.CFurnizimit = (double)item.PurchasePrice;
                    if (item.SalesPrice > 0)
                        artikull.CShitjes = (double)item.SalesPrice;
                }
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            return false;
        }
    }

    /// <summary>Subtracts the stock a set of purchase lines had added.</summary>
    private static async Task ReverseStockAsync(PosDbContext db, List<DitariH> rows)
    {
        foreach (var row in rows)
        {
            if (row.ArtikullId is null) continue;
            var artikull = await db.Artikujt.FirstOrDefaultAsync(a => a.Id == row.ArtikullId);
            if (artikull is null) continue;

            var qty = row.Sasia ?? 0;
            artikull.Sasia = (artikull.Sasia ?? 0) - qty;
            artikull.SasiaHyrje = (artikull.SasiaHyrje ?? 0) - qty;
        }
    }

    /// <summary>The draft behind an existing document, so it can be loaded into the edit form.</summary>
    public async Task<PurchaseDraft?> GetPurchaseDraftAsync(long numri)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.DitariH.AsNoTracking()
            .Where(d => d.Numri == numri)
            .OrderBy(d => d.NrRendor)
            .ToListAsync();
        if (rows.Count == 0) return null;

        var head = rows[0];
        return new PurchaseDraft
        {
            DocumentNumber = head.NrFatures ?? numri.ToString(),
            Date = head.Data ?? DateTime.Now,
            SupplierId = head.Subjekti ?? 0,
            PurchaseType = head.Tipi ?? "Vendore",
            Worker = head.Punetori ?? "",
            IsPaid = (head.Mbeti ?? 0) <= 0,
            Items = rows.Select(d => new PurchaseDraftItem
            {
                ArticleId = d.ArtikullId ?? 0,
                ArticleName = d.Artikulli ?? "",
                Barcode = d.Barkodi ?? "",
                Quantity = (decimal)(d.Sasia ?? 0),
                PurchasePrice = (decimal)(d.CmimiFurn ?? 0),
                SalesPrice = (decimal)(d.CmShitjes ?? 0),
                DiscountPercent = (decimal)(d.RabatiPer ?? 0),
                VATRate = (decimal)(d.TvshPer ?? 0),
            }).ToList()
        };
    }

    /// <summary>Line items of one purchase document, in order.</summary>
    public async Task<List<PurchaseLineView>> GetPurchaseDetailAsync(long numri)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.DitariH.AsNoTracking()
            .Where(d => d.Numri == numri)
            .OrderBy(d => d.NrRendor)
            .Select(d => new PurchaseLineView
            {
                Name = d.Artikulli ?? "",
                Barcode = d.Barkodi ?? "",
                Quantity = (decimal)(d.Sasia ?? 0),
                PurchasePrice = (decimal)(d.CmimiFurn ?? 0),
                Total = (decimal)(d.VleraMeTvsh ?? 0)
            })
            .ToListAsync();
    }
}

public class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>Web-side input for a purchase (the linked PurchaseItem lacks name/sales-price/discount).</summary>
public class PurchaseDraft
{
    public string DocumentNumber { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Now;
    public int SupplierId { get; set; }
    public string PurchaseType { get; set; } = "Vendore";
    public bool IsPaid { get; set; }
    public string? Worker { get; set; }
    public List<PurchaseDraftItem> Items { get; set; } = new();
}

public class PurchaseDraftItem
{
    public int ArticleId { get; set; }
    public string Barcode { get; set; } = "";
    public string ArticleName { get; set; } = "";
    public decimal Quantity { get; set; } = 1;
    public decimal PurchasePrice { get; set; }
    public decimal SalesPrice { get; set; }
    public decimal VATRate { get; set; } = 18;
    public decimal DiscountPercent { get; set; }
}

public class PurchaseSummary
{
    public string DocumentNumber { get; set; } = "";
    public string InvoiceNumber { get; set; } = "";
    public DateTime Date { get; set; }
    public string Time { get; set; } = "";
    public string PurchaseType { get; set; } = "";
    public int ItemCount { get; set; }
    public decimal TotalAmount { get; set; }
}

public class PurchaseLineView
{
    public string Name { get; set; } = "";
    public string Barcode { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal Total { get; set; }
}
