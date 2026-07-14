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

    /// <summary>
    /// The number the next purchase is <em>likely</em> to get — for display only.
    /// Read outside the allocation lock; the number that counts is the one
    /// <see cref="SavePurchaseAsync"/> assigns. Never persist this value.
    /// </summary>
    public async Task<string> PreviewNextDocumentNumberAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var max = await db.DitariH.MaxAsync(d => (long?)d.Numri) ?? 0;
        return (max + 1).ToString();
    }

    /// <summary>
    /// Persists a purchase (journal rows + stock/price update) atomically.
    /// Returns the assigned journal number on success, or null on failure.
    /// The number is allocated under the journal lock — see <see cref="JournalNumber"/>
    /// for why max+1 on its own hands two concurrent entries the same one.
    /// </summary>
    public async Task<long?> SavePurchaseAsync(PurchaseDraft draft)
    {
        if (draft.Items.Count == 0) return null;

        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            await db.LockAsync(JournalNumber.PurchasesKey);

            var maxNumri = await db.DitariH.MaxAsync(d => (long?)d.Numri) ?? 0;
            var nextNumri = maxNumri + 1;
            int nrRendor = 1;

            // Blank means "no supplier invoice number was typed", so the document refers to
            // itself by its journal number. Filling it in here rather than on the screen keeps
            // it equal to the number actually allocated, instead of the one the screen guessed.
            var docNumber = string.IsNullOrWhiteSpace(draft.DocumentNumber)
                ? nextNumri.ToString()
                : draft.DocumentNumber;

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
                    NrFatures = docNumber,
                    NrDUD = docNumber,
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

                var qty = (double)item.Quantity;
                await db.MoveStockAsync(item.ArticleId, delta: qty, inQty: qty);
                await ApplyPurchasePricesAsync(db, item);
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

    /// <summary>
    /// A delivery restates the article's prices. Unlike stock these are facts, not running
    /// totals, so the last delivery in simply wins — there is nothing to accumulate and so
    /// nothing to lose.
    /// </summary>
    private static Task ApplyPurchasePricesAsync(PosDbContext db, PurchaseDraftItem item) =>
        db.Artikujt
            .Where(a => a.Id == item.ArticleId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.CFurnizimit, _ => (double)item.PurchasePrice)
                .SetProperty(a => a.CShitjes, a =>
                    item.SalesPrice > 0 ? (double)item.SalesPrice : a.CShitjes));

    /// <summary>
    /// Every purchase document the shop has, newest first. The screen that shows these has a
    /// search box, and a search that can only see the newest 100 documents is a search that
    /// quietly lies about the other 261.
    /// </summary>
    public Task<List<PurchaseSummary>> GetAllPurchasesAsync() => GetRecentPurchasesAsync(null);

    /// <summary>Recent purchase documents, newest first (mirror of GetRecentSales). Null = all.</summary>
    public async Task<List<PurchaseSummary>> GetRecentPurchasesAsync(int? count = 50)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var numbers = db.DitariH.AsNoTracking()
            .Select(d => d.Numri)
            .Distinct()
            .OrderByDescending(n => n);

        var recentNumbers = count is { } take
            ? await numbers.Take(take).ToListAsync()
            : await numbers.ToListAsync();

        var rows = await db.DitariH.AsNoTracking()
            .Where(d => recentNumbers.Contains(d.Numri))
            .Select(d => new { d.Numri, d.Data, d.Ora, d.NrFatures, d.Tipi, d.VleraMeTvsh,
                               d.Subjekti, d.Mbeti })
            .ToListAsync();

        // The supplier is stored as an id on every journal line and was never resolved to a name,
        // which is why the purchase list could not be searched by the one thing a shop actually
        // looks a delivery up by: who delivered it.
        var suppliers = await db.FurnitoriNew.AsNoTracking()
            .Select(f => new { Id = (int)f.Id, f.Emri })
            .ToDictionaryAsync(f => f.Id, f => f.Emri ?? "");

        return rows
            .GroupBy(d => d.Numri)
            .Select(g =>
            {
                var supplierId = g.Select(d => d.Subjekti).FirstOrDefault(s => s is > 0) ?? 0;
                return new PurchaseSummary
                {
                    DocumentNumber = g.Key?.ToString() ?? "",
                    InvoiceNumber = g.Select(d => d.NrFatures).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "",
                    Date = g.Max(d => d.Data) ?? DateTime.MinValue,
                    Time = g.Select(d => d.Ora).FirstOrDefault(o => !string.IsNullOrEmpty(o)) ?? "",
                    PurchaseType = g.Select(d => d.Tipi).FirstOrDefault(t => !string.IsNullOrEmpty(t)) ?? "",
                    SupplierId = supplierId,
                    SupplierName = supplierId > 0 && suppliers.TryGetValue(supplierId, out var n) ? n : "",
                    ItemCount = g.Count(),
                    TotalAmount = (decimal)g.Sum(d => d.VleraMeTvsh ?? 0),
                    // What the document still owes. The entry form writes it per line, so the
                    // document's outstanding amount is the sum of its lines'.
                    Outstanding = (decimal)g.Sum(d => d.Mbeti ?? 0),
                };
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

            // The number is fixed — this is the same document — so a blank invoice number
            // falls back to it, exactly as it did when the document was first saved.
            var docNumber = string.IsNullOrWhiteSpace(draft.DocumentNumber)
                ? numri.ToString()
                : draft.DocumentNumber;

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
                    NrFatures = docNumber,
                    NrDUD = docNumber,
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

                var qty = (double)item.Quantity;
                await db.MoveStockAsync(item.ArticleId, delta: qty, inQty: qty);
                await ApplyPurchasePricesAsync(db, item);
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

            var qty = row.Sasia ?? 0;
            await db.MoveStockAsync(row.ArtikullId.Value, delta: -qty, inQty: -qty);
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
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = "";
    public int ItemCount { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>Still owed on this document. Zero once it is paid.</summary>
    public decimal Outstanding { get; set; }

    public bool IsPaid => Outstanding <= 0;
}

public class PurchaseLineView
{
    public string Name { get; set; } = "";
    public string Barcode { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal Total { get; set; }
}
