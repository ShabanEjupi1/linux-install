using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Models;
using KosovaPOS.Models.BMDData;

namespace KosovaPOS.Core.Services;

/// <summary>
/// Read access to the product catalogue for the Sale screen.
/// Ported from POS2/Services/ArticleDataService.cs — the source of truth is the
/// BMD <see cref="Artikujt"/> table, mapped to the clean <see cref="Article"/> DTO.
/// Static desktop methods became instance methods over an injected Npgsql factory.
/// </summary>
public class CatalogService
{
    private readonly IDbContextFactory<PosDbContext> _dbFactory;
    private readonly PosCache _cache;

    public CatalogService(IDbContextFactory<PosDbContext> dbFactory, PosCache cache)
    {
        _dbFactory = dbFactory;
        _cache = cache;
    }

    /// <summary>
    /// The whole catalogue, which is how six screens open. Served from memory between writes —
    /// see <see cref="PosCache"/>.
    ///
    /// The cache holds the raw rows and every caller is mapped a fresh set of <see cref="Article"/>
    /// objects. Handing out the same instances would be faster still, and wrong: the Articles
    /// screen binds an edit form straight onto one of them, so an abandoned edit would rewrite
    /// what every other cashier sees the price to be.
    /// </summary>
    public async Task<List<Article>> GetAllArticlesAsync()
    {
        var rows = await CachedRowsAsync();
        return rows.Select(MapToArticle).ToList();
    }

    private async Task<List<Artikujt>> CachedRowsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await _cache.GetOrLoadAsync(db.DatabaseName, DataVersions.Catalog,
            () => db.Artikujt.AsNoTracking().ToListAsync());
    }

    public async Task<List<Article>> SearchArticlesAsync(string searchText, int limit = 30)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return new List<Article>();

        await using var db = await _dbFactory.CreateDbContextAsync();
        var artikujt = await db.Artikujt.AsNoTracking()
            .Where(a => EF.Functions.ILike(a.Barkodi ?? "", $"%{searchText}%") ||
                        EF.Functions.ILike(a.Emertimi ?? "", $"%{searchText}%"))
            .Take(limit)
            .ToListAsync();
        return artikujt.Select(MapToArticle).ToList();
    }

    public async Task<Article?> FindByBarcodeAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var art = await db.Artikujt.AsNoTracking().FirstOrDefaultAsync(a => a.Barkodi == barcode);
        return art is null ? null : MapToArticle(art);
    }

    public async Task<Article?> FindByIdAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var art = await db.Artikujt.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        return art is null ? null : MapToArticle(art);
    }

    public async Task<int> GetArticleCountAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Artikujt.CountAsync();
    }

    /// <summary>
    /// Insert or update an article. Ported from ArticleDataService.SaveArticle,
    /// but the desktop's raw-SQL stock write (needed to satisfy a BMD SQL Server
    /// AFTER-UPDATE trigger) is dropped: Postgres has no such trigger, so stock is
    /// written straight through EF change tracking. Returns the article id.
    /// Exceptions propagate so the UI can surface the real message.
    /// </summary>
    public async Task<int> SaveArticleAsync(Article article)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        // "Shtepi" typed today must land on the "Shtëpi" that 81 articles already use, or the
        // category list grows a second entry that looks identical and splits the shelf in two.
        article.Category = await CanonicalCategoryAsync(db, article.Category);

        var art = article.Id > 0
            ? await db.Artikujt.FirstOrDefaultAsync(a => a.Id == (long)article.Id)
            : null;

        if (art is null)
        {
            art = MapToArtikujt(article);
            db.Artikujt.Add(art);
            await db.SaveChangesAsync();
            return (int)art.Id;
        }

        UpdateArtikujtFromArticle(art, article);
        await db.SaveChangesAsync();
        return (int)art.Id;
    }

    /// <summary>Delete an article by id. Returns false if not found.</summary>
    public async Task<bool> DeleteArticleAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var art = await db.Artikujt.FirstOrDefaultAsync(a => a.Id == id);
        if (art is null) return false;
        db.Artikujt.Remove(art);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// The categories the shop actually uses, one entry each.
    ///
    /// A plain SQL DISTINCT lists "Kozmetike" and "Kozmetikë" as two categories, because to
    /// Postgres they are two strings — but to the shopkeeper reading the dropdown they are one
    /// category printed twice, and picking the wrong twin hides 101 articles. Diacritics are
    /// what a Kosovar keyboard drops when someone is in a hurry, so they cannot be trusted to
    /// tell two categories apart. Variants are folded together and the best-spelled one wins.
    /// </summary>
    public async Task<List<string>> GetCategoriesAsync()
    {
        // Folded from the rows already in memory rather than asked of Postgres again: the
        // Sale screen wants the categories and the articles together, and that was two scans
        // of the same table.
        var raw = (await CachedRowsAsync()).Select(a => a.Kategoria).OfType<string>();

        return raw.Where(c => !string.IsNullOrWhiteSpace(c))
                  .GroupBy(c => CategoryKey(c), StringComparer.Ordinal)
                  .Select(g => PreferredSpelling(g))
                  .OrderBy(c => c, StringComparer.CurrentCulture)
                  .ToList();
    }

    /// <summary>
    /// Folds a category to the key two spellings of the same word share: trimmed, case-blind,
    /// and stripped of the diacritics an Albanian keyboard is most likely to lose.
    /// </summary>
    public static string CategoryKey(string category)
    {
        var s = category.Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s.Normalize(System.Text.NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        // Whitespace too: "Plazh &  Vere" and "Plazh & Vere" are not two shelves.
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Of the spellings a category is stored under, the one to show. The accented one is the
    /// deliberate one — nobody types "ë" by accident — so it wins even when the shop typed the
    /// bare form more often. Between equals, the commonest spelling wins.
    /// </summary>
    private static string PreferredSpelling(IEnumerable<string> variants) =>
        variants.GroupBy(v => v.Trim(), StringComparer.Ordinal)
                .OrderByDescending(g => g.Key.Count(ch => ch is 'ë' or 'ç' or 'Ë' or 'Ç'))
                .ThenByDescending(g => g.Count())
                .First().Key;

    /// <summary>
    /// The spelling this shop already uses for the category being saved, so a re-typed variant
    /// joins the existing shelf instead of forking it. Unknown categories are kept verbatim
    /// (trimmed) — a new category is a legitimate thing to create.
    /// </summary>
    private static async Task<string> CanonicalCategoryAsync(PosDbContext db, string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return "";
        var typed = category.Trim();
        var key = CategoryKey(typed);

        var existing = await db.Artikujt.AsNoTracking()
            .Where(a => a.Kategoria != null && a.Kategoria != "")
            .Select(a => a.Kategoria!)
            .Distinct()
            .ToListAsync();

        var match = existing.Where(c => CategoryKey(c) == key).ToList();
        return match.Count == 0 ? typed : PreferredSpelling(match.Append(typed));
    }

    // Ported verbatim from ArticleDataService.MapToArticle
    private static Article MapToArticle(Artikujt art) => new()
    {
        Id = (int)art.Id,
        Barcode = art.Barkodi ?? "",
        Name = art.Emertimi ?? "",
        Unit = art.NjesiaP ?? "Copë",
        SalesUnit = art.NjesiaSH,
        Pack = (decimal)(art.Paketimi ?? 1),
        PurchasePrice = (decimal)(art.CFurnizimit ?? 0),
        Margin = (decimal)(art.Marzha ?? 0),
        PackagePrice = (decimal)(art.CPaketimit ?? 0),
        WholesalePrice = (decimal)(art.CShumices ?? 0),
        SalesPrice = (decimal)(art.CShitjes ?? 0),
        SalesPrice1 = (decimal)(art.CShitjes1 ?? 0),
        VATRate = KosovoVat.Resolve(art.Vat, art.Tatimi),
        VATType = KosovoVat.ToClass(KosovoVat.Resolve(art.Vat, art.Tatimi)),
        Category = art.Kategoria,
        SupplierId = art.Furnitori ?? 0,
        StockQuantity = (decimal)(art.Sasia ?? 0),
        StockIn = (decimal)(art.SasiaHyrje ?? 0),
        StockOut = (decimal)(art.SasiaDalje ?? 0),
        AverageSalesPrice = (decimal)(art.CMesatarShites ?? 0),
        AveragePurchasePrice = (decimal)(art.CMesatarFurnizues ?? 0),
        ExpiryDate = art.Afati,
        HasBarcode = art.PaBarkod != "Y",
        IsWeighed = art.Peshore ?? false,
        IsActive = true,
        ProductType = art.Tipi ?? 1,
        POSCategoryId = art.KategoriaPosId,
        Notes = art.Verejtje,
        Location = art.Vendi,
        Brand = art.Prodhuesi,
        Importer = art.Importuesi,
        Sector = art.Sektori,
        Branch = art.Filiala?.ToString(),
        PhotoPath = art.PhotoPath
    };

    // Ported from ArticleDataService.MapToArtikujt (insert path).
    private static Artikujt MapToArtikujt(Article a) => new()
    {
        Barkodi = a.Barcode ?? "",
        Emertimi = a.Name ?? "",
        NjesiaP = a.Unit,
        NjesiaSH = a.SalesUnit,
        Paketimi = (double)a.Pack,
        CFurnizimit = (double)a.PurchasePrice,
        Marzha = (double)a.Margin,
        CPaketimit = (double)a.PackagePrice,
        CShumices = (double)a.WholesalePrice,
        CShitjes = (double)a.SalesPrice,
        CShitjes1 = (double)a.SalesPrice1,
        Tatimi = (double)a.VATRate,
        Vat = a.VATType,
        Kategoria = a.Category,
        Furnitori = a.SupplierId > 0 ? a.SupplierId : null,
        Sasia = (double)a.StockQuantity,
        SasiaHyrje = (double)a.StockIn,
        SasiaDalje = (double)a.StockOut,
        CMesatarShites = (double)a.AverageSalesPrice,
        CMesatarFurnizues = (double)a.AveragePurchasePrice,
        Afati = a.ExpiryDate,
        PaBarkod = a.HasBarcode ? "N" : "Y",
        Peshore = a.IsWeighed,
        Tipi = a.ProductType,
        KategoriaPosId = a.POSCategoryId,
        Verejtje = a.Notes,
        Vendi = a.Location,
        Prodhuesi = a.Brand,
        Importuesi = a.Importer,
        Sektori = a.Sector,
        PhotoPath = a.PhotoPath
    };

    // Ported from ArticleDataService.UpdateArtikujtFromArticle (update path).
    private static void UpdateArtikujtFromArticle(Artikujt art, Article a)
    {
        art.Barkodi = a.Barcode ?? "";
        art.Emertimi = a.Name ?? "";
        art.NjesiaP = a.Unit;
        art.NjesiaSH = a.SalesUnit;
        art.Paketimi = (double)a.Pack;
        art.CFurnizimit = (double)a.PurchasePrice;
        art.Marzha = (double)a.Margin;
        art.CPaketimit = (double)a.PackagePrice;
        art.CShumices = (double)a.WholesalePrice;
        art.CShitjes = (double)a.SalesPrice;
        art.CShitjes1 = (double)a.SalesPrice1;
        art.Tatimi = (double)a.VATRate;
        art.Vat = a.VATType;
        art.Kategoria = a.Category;
        art.Furnitori = a.SupplierId > 0 ? a.SupplierId : null;
        art.Sasia = (double)a.StockQuantity;
        art.Afati = a.ExpiryDate;
        art.PaBarkod = a.HasBarcode ? "N" : "Y";
        art.Peshore = a.IsWeighed;
        art.Tipi = a.ProductType;
        art.KategoriaPosId = a.POSCategoryId;
        art.Verejtje = a.Notes;
        art.Vendi = a.Location;
        art.Prodhuesi = a.Brand;
        art.Importuesi = a.Importer;
        art.Sektori = a.Sector;
        art.PhotoPath = a.PhotoPath;
    }
}
