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

    public CatalogService(IDbContextFactory<PosDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<List<Article>> GetAllArticlesAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var artikujt = await db.Artikujt.AsNoTracking().ToListAsync();
        return artikujt.Select(MapToArticle).ToList();
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

    public async Task<List<string>> GetCategoriesAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Artikujt.AsNoTracking()
            .Where(a => a.Kategoria != null && a.Kategoria != "")
            .Select(a => a.Kategoria!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
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
