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

    /// <summary>
    /// An article that is actually for sale online — which means it is also NAMED. Only a
    /// listed article can be ordered, so a seed without a <see cref="ShopListing"/> would be
    /// seeding something the shop does not sell.
    /// </summary>
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

        db.ShopListings.Add(new ShopListing { ArticleId = a.Id, Title = "Kamion druri, i kuq" });
        await db.SaveChangesAsync();

        return a.Id;
    }

    /// <summary>An article that exists in the accounts but was never put on the website.</summary>
    private static async Task<long> SeedUnlistedArticleAsync(IDbContextFactory<PosDbContext> f)
    {
        await using var db = await f.CreateDbContextAsync();
        var a = new Artikujt
        {
            Emertimi = "Loder 0115012",
            Barkodi = $"S{Guid.NewGuid():N}"[..18],
            Sasia = 10,
            CShitjes = 40.00,
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

    /// <summary>
    /// The cart cookie is attacker-authored: it is a list of article ids, and nothing stops
    /// someone putting an id in it that the shop never listed. Existing in <c>Artikujt</c> is
    /// not the same as being for sale — 1619 articles are in that table and only the named,
    /// photographed ones are on the website. If this check is ever dropped, a forged cookie
    /// orders anything in the shop's accounting system.
    /// </summary>
    [Fact]
    public async Task An_article_that_is_not_listed_cannot_be_ordered()
    {
        var factory = new TestFactory();
        var shop = new ShopService(factory, new PosCache());
        var id = await SeedUnlistedArticleAsync(factory);

        var placed = await shop.PlaceOrderAsync([new CartLine(id, 1)], Details(), Settings);

        Assert.False(placed.Ok);
        Assert.Contains("nuk shitet më", placed.Error);
    }

    /// <summary>
    /// The order records what the customer bought, in the words they bought it by. The
    /// accountant's name for the same thing is "Loder 0115012", and it must not surface in an
    /// order, a confirmation email or a thank-you page.
    /// </summary>
    [Fact]
    public async Task An_order_line_carries_the_shop_name_not_the_accounting_name()
    {
        var factory = new TestFactory();
        var shop = new ShopService(factory, new PosCache());
        var id = await SeedArticleAsync(factory, qty: 4, price: 12.00);

        var placed = await shop.PlaceOrderAsync([new CartLine(id, 1)], Details(), Settings);

        Assert.True(placed.Ok, placed.Error);
        Assert.Equal("Kamion druri, i kuq", placed.Order.Items[0].Name);
        Assert.DoesNotContain("shop probe", placed.Order.Items[0].Name);
    }

    private static async Task<double> StockAsync(IDbContextFactory<PosDbContext> f, long id)
    {
        await using var db = await f.CreateDbContextAsync();
        var a = await db.Artikujt.AsNoTracking().FirstAsync(x => x.Id == id);
        return a.Sasia ?? 0;
    }
}
