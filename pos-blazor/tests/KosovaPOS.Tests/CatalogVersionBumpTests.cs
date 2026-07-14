using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Models.BMDData;
using Xunit;

namespace KosovaPOS.Tests;

/// <summary>
/// Stock and price are moved with <c>ExecuteUpdate</c> so two tills cannot lose each
/// other's changes — but <c>ExecuteUpdate</c> bypasses the change tracker, and the
/// catalogue cache is invalidated <em>from</em> the change tracker. So a stock move
/// has to stamp the catalogue version itself; if it does not, a sale changes the
/// database while every screen keeps serving the stock figure from before it.
/// </summary>
[Collection("db")]
public class CatalogVersionBumpTests
{
    private const string Conn =
        "Host=localhost;Port=55432;Database=kosovapos;Username=pos;Password=qapass";

    private static PosDbContext NewCtx() =>
        new(new DbContextOptionsBuilder<PosDbContext>().UseNpgsql(Conn).Options);

    [Fact]
    public async Task Moving_stock_bumps_the_catalogue_version()
    {
        await using var db = NewCtx();
        var a = new Artikujt { Emertimi = "bump probe", Barkodi = $"B{Guid.NewGuid():N}"[..18], Sasia = 5, Vat = 3 };
        db.Artikujt.Add(a);
        await db.SaveChangesAsync();

        var before = DataVersions.Current(db.DatabaseName, DataVersions.Catalog);
        await db.MoveStockAsync(a.Id, delta: -1, outQty: 1);
        var after = DataVersions.Current(db.DatabaseName, DataVersions.Catalog);

        Assert.True(after > before,
            $"catalogue version did not move ({before} -> {after}); the stock cache would go stale");

        // And the write really happened.
        var onHand = (await NewCtx().Artikujt.AsNoTracking().FirstAsync(x => x.Id == a.Id)).Sasia;
        Assert.Equal(4, onHand);
    }
}
