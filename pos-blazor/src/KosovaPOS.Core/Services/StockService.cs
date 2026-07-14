using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Models;

namespace KosovaPOS.Core.Services;

/// <summary>
/// Manual stock movements (hyrje / dalje / inventarizim). Ported from
/// POS/Windows/InventoryManagementWindow.
///
/// Stock lives on the BMD <c>Artikujt</c> row: <c>Sasia</c> is the on-hand
/// quantity, with <c>SasiaHyrje</c>/<c>SasiaDalje</c> as running in/out
/// counters. Every change also appends a <see cref="StockMovement"/> so the
/// figure can be explained afterwards — the desktop only kept a movement row
/// when the (unused) InventoryStocks table was populated, so most desktop
/// stock edits left no trail at all.
/// </summary>
public class StockService
{
    private readonly IDbContextFactory<PosDbContext> _dbFactory;

    public StockService(IDbContextFactory<PosDbContext> dbFactory) => _dbFactory = dbFactory;

    /// <summary>Recent movements, newest first. Optionally scoped to one article.</summary>
    public async Task<List<StockMovement>> GetMovementsAsync(long? articleId = null, int take = 200)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var q = db.StockMovements.AsNoTracking();
        if (articleId is not null)
            q = q.Where(m => m.ArticleId == articleId);
        return await q.OrderByDescending(m => m.MovedAt).ThenByDescending(m => m.Id)
                      .Take(take).ToListAsync();
    }

    /// <summary>Articles whose recorded stock is below zero — always a data fault.</summary>
    public async Task<int> GetNegativeStockCountAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Artikujt.CountAsync(a => (a.Sasia ?? 0) < 0);
    }

    /// <summary>Adds <paramref name="quantity"/> (must be &gt; 0) to stock.</summary>
    public Task<StockMovement> StockInAsync(long articleId, decimal quantity, string? note, string? userName)
        => ApplyAsync(articleId, StockMovementType.In, quantity, note, userName);

    /// <summary>Removes <paramref name="quantity"/> (must be &gt; 0) from stock.</summary>
    public Task<StockMovement> StockOutAsync(long articleId, decimal quantity, string? note, string? userName)
        => ApplyAsync(articleId, StockMovementType.Out, quantity, note, userName);

    /// <summary>
    /// Sets stock to a physically counted figure. The movement records the delta,
    /// so an inventory correction stays auditable against what was there before.
    /// </summary>
    public Task<StockMovement> SetCountedAsync(long articleId, decimal countedQuantity, string? note, string? userName)
        => ApplyAsync(articleId, StockMovementType.Count, countedQuantity, note, userName);

    private async Task<StockMovement> ApplyAsync(
        long articleId, StockMovementType type, decimal quantity, string? note, string? userName)
    {
        if (type != StockMovementType.Count && quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Sasia duhet të jetë më e madhe se zero.");
        if (type == StockMovementType.Count && quantity < 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Sasia e numëruar nuk mund të jetë negative.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        // This is a read-modify-write: a count sets stock to an absolute figure and has to
        // read the previous one to record how far off it was. The transaction alone does not
        // make that safe — at READ COMMITTED a concurrent till sale reads the same starting
        // figure and one of the two movements is simply lost. The row lock is what serialises
        // them; every other stock writer goes through MoveStockAsync, whose UPDATE waits on it.
        await db.LockArticleAsync(articleId);

        var art = await db.Artikujt.FirstOrDefaultAsync(a => a.Id == articleId)
            ?? throw new InvalidOperationException("Artikulli nuk u gjet.");

        var before = (decimal)(art.Sasia ?? 0);
        decimal after, delta;

        switch (type)
        {
            case StockMovementType.In:
                after = before + quantity;
                delta = quantity;
                art.SasiaHyrje = (double)((decimal)(art.SasiaHyrje ?? 0) + quantity);
                break;
            case StockMovementType.Out:
                if (quantity > before)
                    throw new InvalidOperationException(
                        $"Stoku aktual është {before:0.##}; nuk mund të dalin {quantity:0.##}. " +
                        "Për të korrigjuar gjendjen, përdor inventarizimin.");
                after = before - quantity;
                delta = -quantity;
                art.SasiaDalje = (double)((decimal)(art.SasiaDalje ?? 0) + quantity);
                break;
            default: // Count
                after = quantity;
                delta = quantity - before;
                break;
        }

        art.Sasia = (double)after;

        var movement = new StockMovement
        {
            ArticleId = art.Id,
            Barcode = art.Barkodi ?? "",
            ArticleName = art.Emertimi ?? "",
            Unit = art.NjesiaP ?? "Copë",
            Type = type,
            Quantity = delta,
            QuantityBefore = before,
            QuantityAfter = after,
            UnitCost = (decimal)(art.CFurnizimit ?? 0),
            MovedAt = DateTime.Now,
            UserName = userName,
            Note = note,
            Reference = $"WEB-{type.ToString().ToUpperInvariant()}-{DateTime.Now:yyyyMMddHHmmss}"
        };
        db.StockMovements.Add(movement);

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return movement;
    }
}
