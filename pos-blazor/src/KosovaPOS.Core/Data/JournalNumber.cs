using Microsoft.EntityFrameworkCore;

namespace KosovaPOS.Core.Data;

/// <summary>
/// Allocates the next document number for a BMD journal.
///
/// The number is <c>max(Numri) + 1</c>, which is a read-modify-write: two tills
/// finishing a sale in the same instant both read the same max and both write the
/// same number, and neither one fails — the journal simply ends up with two
/// different receipts sharing one number, which no report can tell apart again.
/// <c>MAX()</c> takes no lock and the default isolation is READ COMMITTED, so
/// wrapping the write in a transaction does not prevent this on its own.
///
/// A transaction-scoped advisory lock serialises the allocation: the second till
/// waits for the first to commit, then reads a max that already includes it. The
/// lock is released by COMMIT *and* by ROLLBACK, so a failed sale cannot strand it.
///
/// A Postgres sequence would be the usual answer, but the BMD import writes journal
/// rows straight from the shop's desktop with their own <c>Numri</c> values, and a
/// sequence would drift out of step with them and start handing out numbers the
/// import had already used. <c>max + 1</c> tracks whatever the import brought in.
/// Each business is its own database, so these keys never collide across shops.
/// </summary>
public static class JournalNumber
{
    /// <summary>Advisory-lock key for the sales journal (DitariD).</summary>
    public const long SalesKey = 1;

    /// <summary>Advisory-lock key for the purchases journal (DitariH).</summary>
    public const long PurchasesKey = 2;

    /// <summary>
    /// Takes the allocation lock for <paramref name="journalKey"/>. Must be called
    /// inside the same transaction as the INSERT, or the lock is released before the
    /// row it protects is visible to anyone else.
    /// </summary>
    public static Task LockAsync(this DbContext db, long journalKey, CancellationToken ct = default) =>
        db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", [journalKey], ct);
}

/// <summary>
/// Moves stock on an article.
///
/// Stock used to be moved by loading the article, adjusting <c>Sasia</c> in memory and
/// writing the result back as an absolute number. Two writers who read the same starting
/// figure then overwrite one another, and neither fails: a cashier selling an article
/// while a delivery of it is booked in simply loses one of the two movements, and the
/// figure on the screen drifts away from the shelf with nothing to show why.
///
/// These do the arithmetic in the database — <c>Sasia = Sasia - 3</c>, not
/// <c>Sasia = 97</c> — so the row's own write lock serialises the movements and each one
/// is applied to whatever the previous writer committed.
/// </summary>
public static class StockMovementSql
{
    /// <summary>
    /// Applies a stock delta atomically: negative to sell, positive to receive.
    /// <paramref name="outQty"/>/<paramref name="inQty"/> feed the running
    /// <c>SasiaDalje</c>/<c>SasiaHyrje</c> totals, which are accumulators too.
    /// Must run inside the caller's transaction. A zero delta writes nothing.
    /// </summary>
    public static Task MoveStockAsync(
        this PosDbContext db, long articleId, double delta,
        double inQty = 0, double outQty = 0, CancellationToken ct = default) =>
        db.Artikujt
            .Where(a => a.Id == articleId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Sasia, a => (a.Sasia ?? 0) + delta)
                .SetProperty(a => a.SasiaHyrje, a => (a.SasiaHyrje ?? 0) + inQty)
                .SetProperty(a => a.SasiaDalje, a => (a.SasiaDalje ?? 0) + outQty), ct);

    /// <summary>
    /// Locks an article's row until the transaction ends, for the one caller that cannot
    /// express its change as a delta: an inventory count sets stock to an absolute figure
    /// and has to read the previous one to record how far off it was. Every other writer
    /// goes through <see cref="MoveStockAsync"/>, whose UPDATE waits on this same row lock.
    /// </summary>
    public static Task LockArticleAsync(this PosDbContext db, long articleId, CancellationToken ct = default) =>
        db.Database.ExecuteSqlRawAsync(
            """SELECT 1 FROM "Artikujt" WHERE id = {0} FOR UPDATE""", [articleId], ct);
}
