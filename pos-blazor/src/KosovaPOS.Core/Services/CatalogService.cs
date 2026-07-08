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
        VATRate = (decimal)(art.Tatimi ?? (art.Vat == 3 ? 18 : art.Vat == 2 ? 8 : 0)),
        VATType = art.Vat ?? 3,
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
}
