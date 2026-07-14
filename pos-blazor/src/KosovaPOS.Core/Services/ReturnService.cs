using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Models;

namespace KosovaPOS.Core.Services;

/// <summary>
/// Return / refund (kthim) processing. Ported from POS2/Windows/ReturnWindow.
/// Looks up the original sale in the BMD DitariD journal, records a
/// <see cref="ReturnReceipt"/> with its items, and restocks the returned goods.
/// </summary>
public class ReturnService
{
    private readonly IDbContextFactory<PosDbContext> _dbFactory;

    public ReturnService(IDbContextFactory<PosDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<List<ReturnReceipt>> GetRecentAsync(int count = 50)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.ReturnReceipts.AsNoTracking()
            .OrderByDescending(r => r.ReturnDate)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<ReturnReceiptItem>> GetItemsAsync(int returnId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.ReturnReceiptItems.AsNoTracking()
            .Where(i => i.ReturnReceiptId == returnId)
            .ToListAsync();
    }

    /// <summary>
    /// Lines of the original sale, looked up by its journal number so the cashier
    /// can choose what to return. Empty list if the number is unknown.
    /// </summary>
    public async Task<List<ReceiptLine>> LookupOriginalAsync(string receiptNumber)
    {
        if (!long.TryParse(receiptNumber?.Trim(), out var numri))
            return new List<ReceiptLine>();

        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.DitariD.AsNoTracking()
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
    }

    /// <summary>Next return reference number of the form RET-yyyyMMdd-NNN.</summary>
    public async Task<string> GenerateReturnNumberAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var prefix = $"RET-{DateTime.Today:yyyyMMdd}-";
        var todayCount = await db.ReturnReceipts.CountAsync(r => r.ReturnNumber.StartsWith(prefix));
        return $"{prefix}{(todayCount + 1):000}";
    }

    /// <summary>
    /// Persists a return with its items and, when <paramref name="restock"/> is true,
    /// increments <see cref="KosovaPOS.Models.BMDData.Artikujt"/> stock for each
    /// returned line matched by barcode. Runs in a transaction. Returns the return id.
    /// </summary>
    public async Task<int> SaveReturnAsync(ReturnReceipt ret, bool restock)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        ret.RefundAmount = ret.Items.Sum(i => i.TotalValue);
        db.ReturnReceipts.Add(ret);
        await db.SaveChangesAsync(); // assigns ret.Id, cascades items

        if (restock)
        {
            foreach (var item in ret.Items)
            {
                if (string.IsNullOrWhiteSpace(item.Barcode)) continue;

                // Only the id is read here — the quantity is added in the database, so a till
                // selling this article at the same moment cannot lose the restock.
                var id = await db.Artikujt.AsNoTracking()
                    .Where(a => a.Barkodi == item.Barcode)
                    .Select(a => (long?)a.Id)
                    .FirstOrDefaultAsync();
                if (id is null) continue;

                var qty = (double)item.Quantity;
                await db.MoveStockAsync(id.Value, delta: qty, inQty: qty);
            }
        }

        await tx.CommitAsync();
        return ret.Id;
    }
}
