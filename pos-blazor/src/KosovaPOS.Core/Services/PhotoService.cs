using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Models.Shop;
using System.Net.Http;

namespace KosovaPOS.Core.Services;

public sealed record PhotoResult(ArticlePhoto? Photo, string? Error)
{
    public bool Ok => Error is null;
}

/// <summary>
/// Attaching pictures to articles.
///
/// Every picture is stored as a file under the media root and served from this app.
/// A URL pasted from a supplier's listing is fetched once, here, and written to disk —
/// the address it came from is kept in <see cref="ArticlePhoto.Source"/> and never used
/// again at render time. Serving the remote URL directly would be one line shorter and
/// would put the shop's entire product imagery at the mercy of a host that is free to
/// rate-limit us, block hotlinks, or delete the file; the first quiet morning that
/// happened, every product on enisi.tech would show a broken image and nobody would
/// know why.
/// </summary>
public class PhotoService
{
    /// <summary>
    /// Big enough for any product photo, small enough that a mistyped URL pointing at a
    /// video cannot fill the disk.
    /// </summary>
    public const int MaxBytes = 8 * 1024 * 1024;

    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif", "image/avif"
    };

    private readonly IDbContextFactory<PosDbContext> _dbFactory;
    private readonly IHttpClientFactory _http;
    private readonly MediaStore _media;

    public PhotoService(IDbContextFactory<PosDbContext> dbFactory, IHttpClientFactory http, MediaStore media)
    {
        _dbFactory = dbFactory;
        _http = http;
        _media = media;
    }

    public async Task<List<ArticlePhoto>> GetForArticleAsync(long articleId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.ArticlePhotos.AsNoTracking()
            .Where(p => p.ArticleId == articleId)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();
    }

    /// <summary>How many photos each of these articles has. For the picker's "done" ticks.</summary>
    public async Task<Dictionary<long, int>> CountsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.ArticlePhotos.AsNoTracking()
            .GroupBy(p => p.ArticleId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }

    /// <summary>Download a remote image and attach it.</summary>
    public async Task<PhotoResult> AddFromUrlAsync(long articleId, string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return new PhotoResult(null, "Adresa e fotos është bosh.");

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return new PhotoResult(null, "Adresa duhet të fillojë me http:// ose https://.");

        byte[] bytes;
        string contentType;
        try
        {
            var client = _http.CreateClient(nameof(PhotoService));

            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                return new PhotoResult(null, $"Nuk u shkarkua dot (HTTP {(int)response.StatusCode}).");

            contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!AllowedTypes.Contains(contentType))
                return new PhotoResult(null, $"Kjo adresë nuk është foto ({(contentType.Length == 0 ? "e panjohur" : contentType)}).");

            // Checked before reading, when the server declares it, and again after —
            // Content-Length is a claim, not a guarantee.
            if (response.Content.Headers.ContentLength > MaxBytes)
                return new PhotoResult(null, "Fotoja është më e madhe se 8 MB.");

            bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length > MaxBytes)
                return new PhotoResult(null, "Fotoja është më e madhe se 8 MB.");
        }
        catch (Exception e)
        {
            return new PhotoResult(null, $"Nuk u shkarkua dot: {e.Message}");
        }

        return await AttachAsync(articleId, bytes, contentType, source: uri.ToString(), ct);
    }

    /// <summary>Attach an uploaded file.</summary>
    public async Task<PhotoResult> AddFromUploadAsync(
        long articleId, Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        if (!AllowedTypes.Contains(contentType))
            return new PhotoResult(null, "Lejohen vetëm foto (JPG, PNG, WEBP, GIF).");

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);

        if (buffer.Length == 0)
            return new PhotoResult(null, "Skedari është bosh.");
        if (buffer.Length > MaxBytes)
            return new PhotoResult(null, "Fotoja është më e madhe se 8 MB.");

        return await AttachAsync(articleId, buffer.ToArray(), contentType, source: fileName, ct);
    }

    private async Task<PhotoResult> AttachAsync(
        long articleId, byte[] bytes, string contentType, string source, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var article = await db.Artikujt.AsNoTracking().FirstOrDefaultAsync(a => a.Id == articleId, ct);
        if (article is null)
            return new PhotoResult(null, "Artikulli nuk ekziston.");

        var existing = await db.ArticlePhotos
            .Where(p => p.ArticleId == articleId)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);

        if (existing.Count >= ArticlePhoto.MaxPerArticle)
            return new PhotoResult(null, $"Një artikull mban më së shumti {ArticlePhoto.MaxPerArticle} foto.");

        // The file lands on disk before the row is written. The other order can leave a row
        // pointing at a file that was never created — a permanently broken image on the shop,
        // with nothing to say it should not be there. An orphaned FILE, by contrast, is just
        // some bytes nobody references.
        var url = await _media.SaveAsync(db.DatabaseName, articleId, bytes, contentType, ct);

        var photo = new ArticlePhoto
        {
            ArticleId = articleId,
            Url = url,
            SortOrder = existing.Count == 0 ? 0 : existing.Max(p => p.SortOrder) + 1,
            Source = source.Length > 1000 ? source[..1000] : source,
            CreatedAt = DateTime.Now
        };

        db.ArticlePhotos.Add(photo);
        await db.SaveChangesAsync(ct);

        return new PhotoResult(photo, null);
    }

    public async Task DeleteAsync(int photoId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var photo = await db.ArticlePhotos.FirstOrDefaultAsync(p => p.Id == photoId);
        if (photo is null) return;

        db.ArticlePhotos.Remove(photo);
        await db.SaveChangesAsync();

        // After the row is gone, and failure is ignored: a file we could not delete is
        // wasted disk, while a row whose file we deleted first would be a broken image.
        _media.TryDelete(photo.Url);
    }

    /// <summary>
    /// Make a photo the main one — the picture the grid, the cart and the order email show.
    /// The others keep their order behind it.
    /// </summary>
    public async Task MakeMainAsync(int photoId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var photo = await db.ArticlePhotos.FirstOrDefaultAsync(p => p.Id == photoId);
        if (photo is null) return;

        var siblings = await db.ArticlePhotos
            .Where(p => p.ArticleId == photo.ArticleId)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

        var order = 1;
        foreach (var s in siblings)
            s.SortOrder = s.Id == photoId ? 0 : order++;

        await db.SaveChangesAsync();
    }
}
