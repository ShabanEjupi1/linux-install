using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using KosovaPOS.Core.Data;
using KosovaPOS.Core.Services;
using KosovaPOS.Models;
using Xunit;
using Xunit.Abstractions;

namespace KosovaPOS.Tests;

/// <summary>
/// Two tills finishing a sale at the same moment must not be handed the same
/// receipt number. The number is the fiscal identity of the sale, so a collision
/// is not a cosmetic bug: it makes two different sales indistinguishable in the
/// journal, and any report that groups by it silently merges them.
/// </summary>
public class ReceiptNumberConcurrencyTests
{
    private readonly ITestOutputHelper _out;
    public ReceiptNumberConcurrencyTests(ITestOutputHelper o) => _out = o;

    private const string Conn =
        "Host=localhost;Port=55432;Database=kosovapos;Username=pos;Password=qapass";

    /// <summary>A factory handing out one fresh context per call, as the web app's does.</summary>
    private sealed class TestFactory : IDbContextFactory<PosDbContext>
    {
        private readonly DbContextOptions<PosDbContext> _opts =
            new DbContextOptionsBuilder<PosDbContext>().UseNpgsql(Conn).Options;
        public PosDbContext CreateDbContext() => new(_opts);
    }

    private static IDbContextFactory<PosDbContext> Factory() => new TestFactory();

    private static Receipt CartOf(string cashier, decimal price) => new()
    {
        ReceiptNumber = "pending",
        Date = DateTime.Now,
        CashierName = cashier,
        PaymentMethod = "Cash",
        TotalAmount = price,
        PaidAmount = price,
        LeftAmount = 0,
        Items =
        [
            new ReceiptItem
            {
                ArticleId  = 1,
                Barcode    = "QA-CONC",
                ArticleName= "QA concurrency probe",
                Quantity   = 1,
                Price      = price,
                VATRate    = 18m,
                VATValue   = KosovoVat.VatOf(price, 18m),
                TotalValue = price,
            }
        ]
    };

    [Fact]
    public async Task Two_simultaneous_sales_get_two_different_receipt_numbers()
    {
        var factory = Factory();
        var sales = new SalesService(factory, NullLogger<SalesService>.Instance);

        const int Tills = 8;

        // Every till rings up its sale at the same instant, as two cashiers on a
        // busy Saturday do.
        var barrier = new TaskCompletionSource();
        var runs = Enumerable.Range(0, Tills).Select(async i =>
        {
            await barrier.Task;
            return await sales.SaveReceiptAsync(CartOf($"till{i}", 10m + i));
        }).ToArray();

        barrier.SetResult();
        var assigned = await Task.WhenAll(runs);

        var ok = assigned.Where(n => n is not null).Select(n => n!.Value).ToArray();
        _out.WriteLine($"saved      : {ok.Length}/{Tills}");
        _out.WriteLine($"numbers    : {string.Join(", ", ok.OrderBy(x => x))}");
        _out.WriteLine($"distinct   : {ok.Distinct().Count()}");

        var dupes = ok.GroupBy(x => x).Where(g => g.Count() > 1)
                      .Select(g => $"#{g.Key} x{g.Count()}").ToArray();
        if (dupes.Length > 0)
            _out.WriteLine($"COLLIDED   : {string.Join(", ", dupes)}");

        Assert.Equal(ok.Length, ok.Distinct().Count());
    }
}
