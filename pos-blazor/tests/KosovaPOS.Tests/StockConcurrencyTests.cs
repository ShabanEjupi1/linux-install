using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using KosovaPOS.Core.Data;
using KosovaPOS.Core.Services;
using KosovaPOS.Models;
using KosovaPOS.Models.BMDData;
using Xunit;
using Xunit.Abstractions;

namespace KosovaPOS.Tests;

/// <summary>
/// Stock is moved by read-modify-write: the service reads <c>Sasia</c>, subtracts (or
/// adds) in memory, and writes the result back as an absolute number. Two writers who
/// read the same starting figure therefore overwrite each other, and the shop's stock
/// silently drifts away from what is on the shelf.
///
/// The sales journal lock serialises sales against sales, but a goods-receipt takes a
/// different lock — so a cashier selling an article while the manager books in a
/// delivery of it is exactly the pair that still collides.
/// </summary>
[Collection("db")]
public class StockConcurrencyTests
{
    private readonly ITestOutputHelper _out;
    public StockConcurrencyTests(ITestOutputHelper o) => _out = o;

    private const string Conn =
        "Host=localhost;Port=55432;Database=kosovapos;Username=pos;Password=qapass";

    private sealed class TestFactory : IDbContextFactory<PosDbContext>
    {
        private readonly DbContextOptions<PosDbContext> _opts =
            new DbContextOptionsBuilder<PosDbContext>().UseNpgsql(Conn).Options;
        public PosDbContext CreateDbContext() => new(_opts);
    }

    /// <summary>An article on the shelf with a known starting quantity.</summary>
    private static async Task<int> SeedArticleAsync(IDbContextFactory<PosDbContext> f, double startQty)
    {
        await using var db = await f.CreateDbContextAsync();
        var a = new Artikujt
        {
            Emertimi = "QA stock probe",
            Barkodi = $"QA-STOCK-{Guid.NewGuid():N}"[..20],
            Sasia = startQty,
            CShitjes = 10,
            CFurnizimit = 5,
            Vat = 3,
        };
        db.Artikujt.Add(a);
        await db.SaveChangesAsync();
        return (int)a.Id;
    }

    private static async Task<double> StockOfAsync(IDbContextFactory<PosDbContext> f, int id)
    {
        await using var db = await f.CreateDbContextAsync();
        return (await db.Artikujt.AsNoTracking().FirstAsync(a => a.Id == id)).Sasia ?? 0;
    }

    [Fact]
    public async Task Selling_and_receiving_the_same_article_at_once_keeps_stock_exact()
    {
        var f = new TestFactory();
        var sales = new SalesService(f, NullLogger<SalesService>.Instance);
        var purchases = new PurchaseService(f);

        const int Rounds = 10;
        const double Start = 100;
        var id = await SeedArticleAsync(f, Start);

        // Each round sells one unit and books in one unit, at the same moment.
        // Whatever the interleaving, the shelf ends where it started.
        var gate = new TaskCompletionSource();
        var work = new List<Task>();

        for (int i = 0; i < Rounds; i++)
        {
            work.Add(Task.Run(async () =>
            {
                await gate.Task;
                await sales.SaveReceiptAsync(new Receipt
                {
                    Date = DateTime.Now, CashierName = "qa", PaymentMethod = "Cash",
                    TotalAmount = 10, PaidAmount = 10, LeftAmount = 0,
                    Items = [new ReceiptItem
                    {
                        ArticleId = id, Barcode = "QA-STOCK", ArticleName = "QA stock probe",
                        Quantity = 1, Price = 10m, VATRate = 18m,
                        VATValue = KosovoVat.VatOf(10m, 18m), TotalValue = 10m,
                    }]
                });
            }));

            work.Add(Task.Run(async () =>
            {
                await gate.Task;
                await purchases.SavePurchaseAsync(new PurchaseDraft
                {
                    Date = DateTime.Now, SupplierId = 0, PurchaseType = "Vendore",
                    Worker = "qa", DocumentNumber = "",
                    Items = [new PurchaseDraftItem
                    {
                        ArticleId = id, Barcode = "QA-STOCK", ArticleName = "QA stock probe",
                        Quantity = 1, PurchasePrice = 5m, SalesPrice = 10m, VATRate = 18m,
                    }]
                });
            }));
        }

        gate.SetResult();
        await Task.WhenAll(work);

        var final = await StockOfAsync(f, id);
        _out.WriteLine($"start        : {Start}");
        _out.WriteLine($"sold         : {Rounds} (-{Rounds})");
        _out.WriteLine($"received     : {Rounds} (+{Rounds})");
        _out.WriteLine($"expected     : {Start}");
        _out.WriteLine($"actual       : {final}");
        _out.WriteLine($"LOST UPDATES : {Start - final:+0.##;-0.##;0}");

        Assert.Equal(Start, final);
    }
}
