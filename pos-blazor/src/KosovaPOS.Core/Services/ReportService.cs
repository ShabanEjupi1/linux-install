using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;

namespace KosovaPOS.Core.Services;

/// <summary>
/// Read-only reporting over the BMD journals. Ported from POS2/Services/ZReportService
/// (daily VAT/payment aggregation) and SalesDataService.GetSalesForDateRange, kept as
/// pure queries — no ZReport persistence, no fiscal-device / ATK-export concerns.
/// All aggregation runs in memory (Npgsql-safe, same pattern as the other services).
/// </summary>
public class ReportService
{
    private readonly IDbContextFactory<PosDbContext> _dbFactory;

    public ReportService(IDbContextFactory<PosDbContext> dbFactory) => _dbFactory = dbFactory;

    /// <summary>
    /// Z-report style daily totals: sales with/without VAT, the 18/8/0 VAT split,
    /// cash vs card, and transaction count. Ported from ZReportService.BuildForDate.
    /// </summary>
    public async Task<DailyReport> GetDailyReportAsync(DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);

        await using var db = await _dbFactory.CreateDbContextAsync();
        var lines = await db.DitariD.AsNoTracking()
            .Where(d => d.Data.HasValue && d.Data.Value >= dayStart && d.Data.Value < dayEnd)
            .Select(d => new { d.Numri, d.VleraMeTvsh, d.Tvsh, d.MetodaP })
            .ToListAsync();

        var report = new DailyReport { Date = dayStart };

        foreach (var l in lines)
        {
            decimal total = (decimal)(l.VleraMeTvsh ?? 0);
            decimal vat = (decimal)(l.Tvsh ?? 0);
            decimal net = total - vat;

            // Bucket by the effective VAT rate implied by vat/net (ZReport heuristic).
            if (vat > 0 && net > 0)
            {
                double ratio = (double)(vat / net) * 100;
                if (ratio > 14) { report.Base18 += net; report.Vat18 += vat; }
                else { report.Base8 += net; report.Vat8 += vat; }
            }
            else
            {
                report.Base0 += total;
            }

            // MetodaP: 2 = card, everything else counts as cash.
            if (l.MetodaP == 2) report.Card += total;
            else report.Cash += total;
        }

        report.TotalWithVat = (decimal)lines.Sum(l => l.VleraMeTvsh ?? 0);
        report.TotalVat = report.Vat18 + report.Vat8;
        report.TotalWithoutVat = report.TotalWithVat - report.TotalVat;
        report.Transactions = lines.Where(l => l.Numri.HasValue).Select(l => l.Numri!.Value).Distinct().Count();
        report.ItemLines = lines.Count;
        if (report.Cash == 0 && report.Card == 0) report.Cash = report.TotalWithVat;
        return report;
    }

    /// <summary>
    /// Sales + purchases totals over an inclusive date range, with a per-day sales
    /// breakdown and the top-selling articles by revenue.
    /// </summary>
    public async Task<RangeSummary> GetRangeSummaryAsync(DateTime from, DateTime to)
    {
        var start = from.Date;
        var end = to.Date.AddDays(1);

        await using var db = await _dbFactory.CreateDbContextAsync();

        var sales = await db.DitariD.AsNoTracking()
            .Where(d => d.Data.HasValue && d.Data.Value >= start && d.Data.Value < end)
            .Select(d => new { d.Numri, d.Data, d.Artikulli, d.Sasia, d.VleraMeTvsh, d.Tvsh })
            .ToListAsync();

        var purchasesTotal = await db.DitariH.AsNoTracking()
            .Where(d => d.Data.HasValue && d.Data.Value >= start && d.Data.Value < end)
            .SumAsync(d => (double?)d.VleraMeTvsh) ?? 0;

        var summary = new RangeSummary
        {
            From = start,
            To = to.Date,
            SalesTotal = (decimal)sales.Sum(s => s.VleraMeTvsh ?? 0),
            SalesVat = (decimal)sales.Sum(s => s.Tvsh ?? 0),
            Transactions = sales.Where(s => s.Numri.HasValue).Select(s => s.Numri!.Value).Distinct().Count(),
            ItemsSold = sales.Count,
            PurchasesTotal = (decimal)purchasesTotal,
            Days = sales
                .Where(s => s.Data.HasValue)
                .GroupBy(s => s.Data!.Value.Date)
                .Select(g => new DayRow
                {
                    Date = g.Key,
                    Total = (decimal)g.Sum(x => x.VleraMeTvsh ?? 0),
                    Transactions = g.Where(x => x.Numri.HasValue).Select(x => x.Numri!.Value).Distinct().Count()
                })
                .OrderBy(d => d.Date)
                .ToList(),
            TopArticles = sales
                .GroupBy(s => s.Artikulli ?? "")
                .Select(g => new ArticleRow
                {
                    Name = g.Key,
                    Quantity = (decimal)g.Sum(x => x.Sasia ?? 0),
                    Revenue = (decimal)g.Sum(x => x.VleraMeTvsh ?? 0)
                })
                .OrderByDescending(a => a.Revenue)
                .Take(10)
                .ToList()
        };
        return summary;
    }
}

public class DailyReport
{
    public DateTime Date { get; set; }
    public decimal TotalWithVat { get; set; }
    public decimal TotalWithoutVat { get; set; }
    public decimal TotalVat { get; set; }
    public decimal Base18 { get; set; }
    public decimal Vat18 { get; set; }
    public decimal Base8 { get; set; }
    public decimal Vat8 { get; set; }
    public decimal Base0 { get; set; }
    public decimal Cash { get; set; }
    public decimal Card { get; set; }
    public int Transactions { get; set; }
    public int ItemLines { get; set; }
}

public class RangeSummary
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public decimal SalesTotal { get; set; }
    public decimal SalesVat { get; set; }
    public int Transactions { get; set; }
    public int ItemsSold { get; set; }
    public decimal PurchasesTotal { get; set; }
    public List<DayRow> Days { get; set; } = new();
    public List<ArticleRow> TopArticles { get; set; } = new();
}

public class DayRow
{
    public DateTime Date { get; set; }
    public decimal Total { get; set; }
    public int Transactions { get; set; }
}

public class ArticleRow
{
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Revenue { get; set; }
}
