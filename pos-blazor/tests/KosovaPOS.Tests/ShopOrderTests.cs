using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Core.Services;
using KosovaPOS.Models;
using KosovaPOS.Models.BMDData;
using KosovaPOS.Models.Shop;
using Xunit;

namespace KosovaPOS.Tests;

/// <summary>
/// Web orders against the real database: the two behaviours that move money and stock.
/// </summary>
[Collection("db")]
public class ShopOrderTests
{
    private const string Conn =
        "Host=localhost;Port=55432;Database=kosovapos;Username=pos;Password=qapass";

    private sealed class TestFactory : IDbContextFactory<PosDbContext>
    {
        private readonly DbContextOptions<PosDbContext> _opts =
            new DbContextOptionsBuilder<PosDbContext>().UseNpgsql(Conn).Options;
        public PosDbContext CreateDbContext() => new(_opts);
    }

    private static readonly BusinessSettings Settings = new()
    {
        ShopEnabled = true,
        ShopShippingFee = 2.00m,
        ShopFreeShippingOver = 0m,
    };

    private static async Task<long> SeedArticleAsync(IDbContextFactory<PosDbContext> f, double qty, double price)
    {
        await using var db = await f.CreateDbContextAsync();
        var a = new Artikujt
        {
            Emertimi = "shop probe",
            Barkodi = $"S{Guid.NewGuid():N}"[..18],
            Sasia = qty,
            CShitjes = price,
            Vat = 3,
        };
        db.Artikujt.Add(a);
        await db.SaveChangesAsync();
        return a.Id;
    }

    private static WebOrder Details() => new()
    {
        CustomerName = "Test Blerësi",
        Email = "test@example.com",
        Address = "Rr. Test 1",
        PaymentMethod = WebPaymentMethod.CashOnDelivery,
    };

    /// <summary>
    /// The cart is a cookie. If the price it names were ever trusted, the shop would sell a
    /// €40 coat for a cent to anyone who can edit a cookie — so the cart carries no price at
    /// all, and this pins that the charge comes from the article row.
    /// </summary>
    [Fact]
    public async Task An_order_is_priced_from_the_database_not_the_cart()
    {
        var factory = new TestFactory();
        var shop = new ShopService(factory, new PosCache());

        var id = await SeedArticleAsync(factory, qty: 10, price: 40.00);

        // The cart can only ever say "two of article N". There is nowhere in CartLine to put
        // a price — which is the point being asserted.
        var placed = await shop.PlaceOrderAsync([new CartLine(id, 2)], Details(), Settings);

        Assert.True(placed.Ok, placed.Error);
        Assert.Equal(40.00m, placed.Order.Items[0].UnitPrice);
        Assert.Equal(80.00m, placed.Order.Subtotal);
        Assert.Equal(2.00m, placed.Order.ShippingFee);
        Assert.Equal(82.00m, placed.Order.Total);
    }

    [Fact]
    public async Task An_order_for_more_than_is_in_stock_is_refused()
    {
        var factory = new TestFactory();
        var shop = new ShopService(factory, new PosCache());

        var id = await SeedArticleAsync(factory, qty: 3, price: 5.00);

        var placed = await shop.PlaceOrderAsync([new CartLine(id, 4)], Details(), Settings);

        Assert.False(placed.Ok);
        Assert.Contains("gjendje", placed.Error);
    }

    /// <summary>
    /// PayPal calls a return URL more than once — the customer refreshes it, the browser
    /// retries it. Stock must come off exactly once. Without the StockTaken flag, a refresh
    /// on the thank-you page silently sells the shop's inventory twice.
    /// </summary>
    [Fact]
    public async Task Confirming_an_order_twice_takes_stock_once()
    {
        var factory = new TestFactory();
        var shop = new ShopService(factory, new PosCache());

        var id = await SeedArticleAsync(factory, qty: 10, price: 5.00);

        var placed = await shop.PlaceOrderAsync([new CartLine(id, 3)], Details(), Settings);
        Assert.True(placed.Ok, placed.Error);

        // Placing the order alone must not move stock: an unpaid basket may never reserve
        // goods, or anyone could empty the shop by filling carts they never pay for.
        Assert.Equal(10, await StockAsync(factory, id));

        await shop.ConfirmAsync(placed.Order.Id, WebPaymentStatus.Paid, "CAP-1");
        Assert.Equal(7, await StockAsync(factory, id));

        // The replay.
        await shop.ConfirmAsync(placed.Order.Id, WebPaymentStatus.Paid, "CAP-1");
        Assert.Equal(7, await StockAsync(factory, id));
    }

    private static async Task<double> StockAsync(IDbContextFactory<PosDbContext> f, long id)
    {
        await using var db = await f.CreateDbContextAsync();
        var a = await db.Artikujt.AsNoTracking().FirstAsync(x => x.Id == id);
        return a.Sasia ?? 0;
    }
}
