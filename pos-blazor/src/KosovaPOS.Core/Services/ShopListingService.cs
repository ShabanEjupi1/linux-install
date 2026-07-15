using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Models.Shop;

namespace KosovaPOS.Core.Services;

/// <summary>
/// Reading and writing the customer-facing name of an article.
///
/// Deliberately the only write path to <see cref="ShopListing"/>. See that type for why
/// the name lives in its own table instead of in <c>Artikujt.Emertimi</c> — the short
/// version is that the BMD import overwrites <c>Emertimi</c> wholesale, so a name typed
/// into the POS would be silently reverted on the next import.
/// </summary>
public class ShopListingService
{
    private readonly IDbContextFactory<PosDbContext> _dbFactory;

    public ShopListingService(IDbContextFactory<PosDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<ShopListing?> GetAsync(long articleId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.ShopListings.AsNoTracking().FirstOrDefaultAsync(l => l.ArticleId == articleId);
    }

    /// <summary>Every article that has been given a real name, by id. Used for the progress count.</summary>
    public async Task<Dictionary<long, string>> TitlesAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.ShopListings.AsNoTracking()
            .Where(l => l.Title != null && l.Title != "")
            .ToDictionaryAsync(l => l.ArticleId, l => l.Title!);
    }

    /// <summary>
    /// Writes the name and description for one article, creating the listing if this is the
    /// first time anyone has named it.
    ///
    /// Saved through the change tracker rather than <c>ExecuteUpdate</c>: the cache stamp that
    /// puts a product on the website is bumped from <c>SaveChanges</c>, and an ExecuteUpdate
    /// would skip it — the article would be named in the database and still nameless on
    /// enisi.tech until the cache happened to expire. That exact bypass has bitten this
    /// codebase once already.
    /// </summary>
    public async Task SaveAsync(long articleId, string? title, string? description, string? updatedBy)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var listing = await db.ShopListings.FirstOrDefaultAsync(l => l.ArticleId == articleId);
        if (listing is null)
        {
            listing = new ShopListing { ArticleId = articleId };
            db.ShopListings.Add(listing);
        }

        listing.Title = Clean(title, 200);
        listing.Description = Clean(description, 2000);
        listing.UpdatedBy = updatedBy;
        listing.UpdatedAt = DateTime.Now;

        await db.SaveChangesAsync();
    }

    /// <summary>Blank is stored as NULL, so "no name" is one state in the database and not two.</summary>
    private static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }
}
