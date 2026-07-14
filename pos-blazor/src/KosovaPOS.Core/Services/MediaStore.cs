using System.Security.Cryptography;

namespace KosovaPOS.Core.Services;

/// <summary>
/// Where product photos live on disk, and the URLs they are served under.
///
/// Files are laid out per business database — <c>{media}/{database}/{name}.jpg</c> — so one
/// shop's pictures cannot be reached by guessing another's URLs, and so a business can be
/// deleted by deleting a directory. That mirrors the database-per-business boundary the
/// rest of the app is built on rather than inventing a second, weaker one.
///
/// The filename is content-addressed: the SHA-256 of the bytes. Uploading the same picture
/// twice writes one file, and — the part that matters — a filename can never be derived
/// from anything a user typed, so no crafted name can walk out of the media root.
/// </summary>
public sealed class MediaStore
{
    public const string UrlPrefix = "/media";

    private readonly string _root;

    public MediaStore(string root)
    {
        _root = root;
        Directory.CreateDirectory(_root);
    }

    /// <summary>The directory to serve <see cref="UrlPrefix"/> from.</summary>
    public string Root => _root;

    public async Task<string> SaveAsync(
        string database, long articleId, byte[] bytes, string contentType, CancellationToken ct = default)
    {
        var extension = contentType.ToLowerInvariant() switch
        {
            "image/png"  => ".png",
            "image/webp" => ".webp",
            "image/gif"  => ".gif",
            "image/avif" => ".avif",
            _            => ".jpg",
        };

        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()[..32];
        var fileName = $"{articleId}-{hash}{extension}";

        var dir = Path.Combine(_root, SafeSegment(database));
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path))
            await File.WriteAllBytesAsync(path, bytes, ct);

        return $"{UrlPrefix}/{SafeSegment(database)}/{fileName}";
    }

    /// <summary>
    /// Deletes the file a stored URL points at. Best-effort: the row is already gone, and a
    /// leftover file is wasted bytes rather than a bug the shop can see.
    /// </summary>
    public void TryDelete(string url)
    {
        try
        {
            if (!url.StartsWith(UrlPrefix + "/", StringComparison.Ordinal))
                return;

            var relative = url[(UrlPrefix.Length + 1)..];
            var path = Path.GetFullPath(Path.Combine(_root, relative));

            // The URL comes out of our own database, but resolving it against the root and
            // checking it landed inside costs nothing and means a bad row cannot delete
            // a file elsewhere on the disk.
            if (!path.StartsWith(Path.GetFullPath(_root) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                return;

            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Nothing useful to do: the photo is already detached from the article.
        }
    }

    /// <summary>A database name is already a validated Postgres identifier, but this is the
    /// one place it becomes a path, so it is filtered rather than trusted.</summary>
    private static string SafeSegment(string value)
        => new(value.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray());
}
