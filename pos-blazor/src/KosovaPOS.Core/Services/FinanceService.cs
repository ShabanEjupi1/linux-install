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

    public record CashEntry(long Id, string Kind, DateTime Date, string Description, string Doc, decimal Amount);

    /// <summary>Income + expense entries in a date range, newest first.
    /// Reads the BMD <c>ArkaHyrjeDalje</c> ledger — the main cash-movements table
    /// (VleraH = income / VleraD = expense), matching the desktop FinanceWindow.</summary>
    public async Task<List<CashEntry>> GetCashBookAsync(DateTime from, DateTime to)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var end = to.Date.AddDays(1);

        var rows = await db.Set<ArkaHyrjeDalje>().AsNoTracking()
            .Where(a => a.Data >= from.Date && a.Data < end
                        && ((a.VleraH ?? 0) > 0 || (a.VleraD ?? 0) > 0))
            .Select(a => new { a.Id, a.Data, a.Pershkrimi, a.DOK, a.VleraH, a.VleraD })
            .ToListAsync();

        var entries = new List<CashEntry>(rows.Count);
        foreach (var a in rows)
        {
            var date = a.Data ?? DateTime.MinValue;
            if ((a.VleraH ?? 0) > 0)
                entries.Add(new CashEntry(a.Id, "Hyrje", date, a.Pershkrimi ?? "", a.DOK ?? "", (decimal)(a.VleraH ?? 0)));
            if ((a.VleraD ?? 0) > 0)
                entries.Add(new CashEntry(a.Id, "Dalje", date, a.Pershkrimi ?? "", a.DOK ?? "", (decimal)(a.VleraD ?? 0)));
        }
        return entries.OrderByDescending(e => e.Date).ThenByDescending(e => e.Id).ToList();
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
        db.Set<ArkaHyrjeDalje>().Add(new ArkaHyrjeDalje
        {
            Pershkrimi = Compose(description, worker), DOK = "MANUAL", Data = DateTime.Now,
            Subjekti = 0, MetodaP = 1, VleraH = (double)amount, VleraD = 0
        });
        await db.SaveChangesAsync();
    }

    public async Task AddExpenseAsync(string description, string worker, decimal amount)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Set<ArkaHyrjeDalje>().Add(new ArkaHyrjeDalje
        {
            Pershkrimi = Compose(description, worker), DOK = "MANUAL", Data = DateTime.Now,
            Subjekti = 0, MetodaP = 1, VleraH = 0, VleraD = (double)amount
        });
        await db.SaveChangesAsync();
    }

    // ArkaHyrjeDalje has no worker column; keep the operator in the description.
    private static string Compose(string description, string worker) =>
        string.IsNullOrWhiteSpace(worker) ? description : $"{description} ({worker})";

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
