using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Models;

namespace KosovaPOS.Core.Services;

/// <summary>
/// Cashier shift (arka) lifecycle. Ported from POS2/Windows/OpenShiftWindow +
/// CloseShiftWindow + CashRegisterWindow. One <see cref="CashShift"/> row per
/// opened shift; the register is expected to run against the single open shift.
/// Cash/card totals for the shift come from the BMD DitariD sales journal.
/// </summary>
public class ShiftService
{
    private readonly IDbContextFactory<PosDbContext> _dbFactory;

    public ShiftService(IDbContextFactory<PosDbContext> dbFactory) => _dbFactory = dbFactory;

    /// <summary>The currently open shift, or null if the register is closed.</summary>
    public async Task<CashShift?> GetOpenShiftAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.CashShifts.AsNoTracking()
            .Where(s => s.Status == ShiftStatus.Open)
            .OrderByDescending(s => s.OpenedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<List<CashShift>> GetRecentAsync(int count = 30)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.CashShifts.AsNoTracking()
            .OrderByDescending(s => s.OpenedAt)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>
    /// Opens a new shift with the given starting float. Throws if one is already open.
    /// </summary>
    public async Task<CashShift> OpenShiftAsync(decimal openingCash, int? cashierId, string? cashierName)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        if (await db.CashShifts.AnyAsync(s => s.Status == ShiftStatus.Open))
            throw new InvalidOperationException("Ekziston tashmë një ndërrim i hapur. Mbylle atë së pari.");

        var shift = new CashShift
        {
            OpenedAt = DateTime.Now,
            OpeningCash = openingCash,
            CashierId = cashierId,
            CashierName = cashierName,
            Status = ShiftStatus.Open
        };
        db.CashShifts.Add(shift);
        await db.SaveChangesAsync();
        return shift;
    }

    /// <summary>
    /// Cash / card / total sales recorded since a shift opened, from the DitariD
    /// journal. MetodaP: 1 = cash, everything else is treated as card/other.
    /// </summary>
    public async Task<(decimal cash, decimal card, decimal total, int count)> GetShiftSalesAsync(DateTime since)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.DitariD.AsNoTracking()
            .Where(d => d.Data >= since)
            .Select(d => new { d.Numri, d.VleraMeTvsh, d.MetodaP })
            .ToListAsync();

        var cash = (decimal)rows.Where(r => (r.MetodaP ?? 1) == 1).Sum(r => r.VleraMeTvsh ?? 0);
        var card = (decimal)rows.Where(r => (r.MetodaP ?? 1) != 1).Sum(r => r.VleraMeTvsh ?? 0);
        var count = rows.Select(r => r.Numri).Distinct().Count();
        return (cash, card, cash + card, count);
    }

    /// <summary>
    /// Closes the open shift: records counted cash, computes expected cash
    /// (opening + cash sales) and the difference, and snapshots the sales totals.
    /// </summary>
    public async Task<CashShift> CloseShiftAsync(int shiftId, decimal countedCash, string? notes, string? denominationJson)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var shift = await db.CashShifts.FirstOrDefaultAsync(s => s.Id == shiftId)
            ?? throw new InvalidOperationException("Ndërrimi nuk u gjet.");
        if (shift.Status != ShiftStatus.Open)
            throw new InvalidOperationException("Ky ndërrim është tashmë i mbyllur.");

        var (cash, card, total, count) = await GetShiftSalesAsync(shift.OpenedAt);

        shift.ClosedAt = DateTime.Now;
        shift.ClosingCash = countedCash;
        shift.TotalSales = total;
        shift.TotalCashSales = cash;
        shift.TotalCardSales = card;
        shift.TransactionCount = count;
        shift.ExpectedCash = shift.OpeningCash + cash;
        shift.CashDifference = countedCash - shift.ExpectedCash;
        shift.Notes = notes;
        shift.DenominationCountJson = denominationJson;
        shift.Status = ShiftStatus.Closed;

        await db.SaveChangesAsync();
        return shift;
    }
}
