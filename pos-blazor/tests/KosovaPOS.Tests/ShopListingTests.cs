using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Core.Services;
using KosovaPOS.Models.BMDData;
using KosovaPOS.Models.Shop;
using Xunit;

namespace KosovaPOS.Tests;

/// <summary>
/// What the website is allowed to show, and what it calls it.
///
/// The shop's articles arrive from BMD with no picture and an accountant's name
/// ("Loder 0115012" — <i>Toy 0115012</i>). Neither half sells anything on its own, so an
/// article is on the website only once a human has supplied both.
/// </summary>
[Collection("db")]
public class ShopListingTests
{
    private const string Conn =
        "Host=localhost;Port=55432;Database=kosovapos;Username=pos;Password=qapass";

    private sealed class TestFactory : IDbContextFactory<PosDbContext>
    {
        private readonly DbContextOptions<PosDbContext> _opts =
            new DbContextOptionsBuilder<PosDbContext>().UseNpgsql(Conn).Options;
        public PosDbContext CreateDbContext() => new(_opts);
    }

    private static async Task<long> SeedArticleAsync(IDbContextFactory<PosDbContext> f)
    {
        await using var db = await f.CreateDbContextAsync();
        var a = new Artikujt
        {
            Emertimi = "Loder 0115012",
            Barkodi = $"L{Guid.NewGuid():N}"[..18],
            Sasia = 3,
            CShitjes = 9.90,
            Vat = 3,
        };
        db.Artikujt.Add(a);
        await db.SaveChangesAsync();
        return a.Id;
    }

    private static async Task PhotographAsync(IDbContextFactory<PosDbContext> f, long id)
    {
        await using var db = await f.CreateDbContextAsync();
        db.ArticlePhotos.Add(new ArticlePhoto { ArticleId = id, Url = "/media/probe.jpg", SortOrder = 0 });
        await db.SaveChangesAsync();
    }

    private static async Task<ShopProduct?> ListedAsync(IDbContextFactory<PosDbContext> f, long id)
        => await new ShopService(f, new PosCache()).GetProductAsync(id);

    [Fact]
    public async Task A_photographed_article_with_no_name_is_not_on_the_website()
    {
        var factory = new TestFactory();
        var id = await SeedArticleAsync(factory);
        await PhotographAsync(factory, id);

        // A picture and the title "Loder 0115012" is not a product listing, it is a bug report.
        Assert.Null(await ListedAsync(factory, id));
    }

    [Fact]
    public async Task A_named_article_with_no_photo_is_not_on_the_website()
    {
        var factory = new TestFactory();
        var listings = new ShopListingService(factory);
        var id = await SeedArticleAsync(factory);

        await listings.SaveAsync(id, "Kamion druri, i kuq", null, "test");

        Assert.Null(await ListedAsync(factory, id));
    }

    [Fact]
    public async Task An_article_with_both_is_on_the_website_under_its_shop_name()
    {
        var factory = new TestFactory();
        var listings = new ShopListingService(factory);
        var id = await SeedArticleAsync(factory);

        await PhotographAsync(factory, id);
        await listings.SaveAsync(id, "Kamion druri, i kuq", "Lodër prej druri masiv.", "test");

        var product = await ListedAsync(factory, id);

        Assert.NotNull(product);
        Assert.Equal("Kamion druri, i kuq", product!.Name);
        Assert.Equal("Lodër prej druri masiv.", product.Description);
        // The customer must never be shown the accounting name.
        Assert.DoesNotContain("0115012", product.Name);
    }

    /// <summary>
    /// The whole reason the name lives in its own table.
    ///
    /// <c>Artikujt</c> is a LOOKUP for the BMD import, which upserts it with
    /// <c>ON CONFLICT (id) DO UPDATE SET</c> every column from the shop PC's export. Anything
    /// written into <c>Emertimi</c> is therefore reverted by the next import — silently, since
    /// the row still exists and still looks fine. If naming an article ever starts touching
    /// <c>Emertimi</c>, a day of naming work will vanish the next time the shop syncs, so this
    /// test guards the boundary rather than the wording.
    /// </summary>
    [Fact]
    public async Task Naming_an_article_for_the_shop_does_not_touch_the_accounting_name()
    {
        IDbContextFactory<PosDbContext> factory = new TestFactory();
        var listings = new ShopListingService(factory);
        var id = await SeedArticleAsync(factory);

        await listings.SaveAsync(id, "Kamion druri, i kuq", null, "test");

        await using var db = await factory.CreateDbContextAsync();
        var article = await db.Artikujt.AsNoTracking().FirstAsync(a => a.Id == id);

        Assert.Equal("Loder 0115012", article.Emertimi);
    }

    /// <summary>
    /// Naming an article puts it on the website immediately. The shop catalogue is cached and
    /// the stamp is bumped from <c>SaveChanges</c>, so a write path that skipped the change
    /// tracker (an <c>ExecuteUpdate</c>, say) would name the article in the database and leave
    /// it nameless — and therefore absent — on enisi.tech until the cache happened to expire.
    /// That bypass has already shipped once in this codebase.
    /// </summary>
    [Fact]
    public async Task Naming_an_article_rebuilds_the_shop_catalogue()
    {
        IDbContextFactory<PosDbContext> factory = new TestFactory();
        var listings = new ShopListingService(factory);
        var id = await SeedArticleAsync(factory);
        await PhotographAsync(factory, id);

        await using var db = await factory.CreateDbContextAsync();
        var before = DataVersions.Current(db.DatabaseName, DataVersions.ShopCatalog);

        await listings.SaveAsync(id, "Kamion druri, i kuq", null, "test");

        var after = DataVersions.Current(db.DatabaseName, DataVersions.ShopCatalog);

        Assert.True(after > before,
            $"shop catalogue version did not move ({before} -> {after}); the named product would stay off the website");
    }
}
