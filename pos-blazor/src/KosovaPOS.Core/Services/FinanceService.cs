using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Models.BMDData;

namespace KosovaPOS.Core.Services;

/// <summary>
/// Cash-book (arka) income/expenses and customer debts (borxhi).
/// Ported from POS2/Windows/FinanceWindow over the BMD ArkaHyrje / ArkaDalje /
/// Borxhi tables. All amounts are euro.
/// </summary>
public class FinanceService
{
    private readonly IDbContextFactory<PosDbContext> _dbFactory;

    public FinanceService(IDbContextFactory<PosDbContext> dbFactory) => _dbFactory = dbFactory;

    public record CashEntry(long Id, string Kind, DateTime Date, string Description, string Worker, decimal Amount);

    /// <summary>Income + expense entries in a date range, newest first.</summary>
    public async Task<List<CashEntry>> GetCashBookAsync(DateTime from, DateTime to)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var end = to.Date.AddDays(1);

        var income = await db.Set<ArkaHyrje>().AsNoTracking()
            .Where(h => h.Data >= from.Date && h.Data < end)
            .Select(h => new CashEntry(h.Id, "Hyrje", h.Data ?? DateTime.MinValue,
                h.Pershkrimi ?? "", h.Punetori ?? "", (decimal)(h.Vlera ?? 0)))
            .ToListAsync();

        var expense = await db.Set<ArkaDalje>().AsNoTracking()
            .Where(d => d.Data >= from.Date && d.Data < end)
            .Select(d => new CashEntry(d.Id, "Dalje", d.Data ?? DateTime.MinValue,
                d.Pershkrimi ?? "", d.Punetori ?? "", (decimal)(d.Vlera ?? 0)))
            .ToListAsync();

        return income.Concat(expense).OrderByDescending(e => e.Date).ToList();
    }

    /// <summary>(income, expense, net) totals in the range.</summary>
    public async Task<(decimal income, decimal expense, decimal net)> GetTotalsAsync(DateTime from, DateTime to)
    {
        var entries = await GetCashBookAsync(from, to);
        var inc = entries.Where(e => e.Kind == "Hyrje").Sum(e => e.Amount);
        var exp = entries.Where(e => e.Kind == "Dalje").Sum(e => e.Amount);
        return (inc, exp, inc - exp);
    }

    public async Task AddIncomeAsync(string description, string worker, decimal amount)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Set<ArkaHyrje>().Add(new ArkaHyrje
        {
            Pershkrimi = description, Punetori = worker, Data = DateTime.Now, Vlera = (double)amount
        });
        await db.SaveChangesAsync();
    }

    public async Task AddExpenseAsync(string description, string worker, decimal amount)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Set<ArkaDalje>().Add(new ArkaDalje
        {
            Pershkrimi = description, Punetori = worker, Data = DateTime.Now, Vlera = (double)amount
        });
        await db.SaveChangesAsync();
    }

    // ── Debts (Borxhi) ──────────────────────────────────────────────────────

    public async Task<List<Borxhi>> GetDebtsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Set<Borxhi>().AsNoTracking()
            .Where(b => (b.Mbeti ?? 0) > 0)
            .OrderByDescending(b => b.Data)
            .ToListAsync();
    }

    public async Task<decimal> TotalDebtAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return (decimal)(await db.Set<Borxhi>().SumAsync(b => (double?)b.Mbeti) ?? 0);
    }

    /// <summary>Records a payment against a debt, reducing the remaining balance.</summary>
    public async Task<bool> RecordDebtPaymentAsync(long debtId, decimal payment)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var debt = await db.Set<Borxhi>().FirstOrDefaultAsync(b => b.Id == debtId);
        if (debt is null) return false;
        debt.Pagoi = (debt.Pagoi ?? 0) + (double)payment;
        debt.Mbeti = Math.Max(0, (debt.Vlera ?? 0) - (debt.Pagoi ?? 0));
        await db.SaveChangesAsync();
        return true;
    }
}
