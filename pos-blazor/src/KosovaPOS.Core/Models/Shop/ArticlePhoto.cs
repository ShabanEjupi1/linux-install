using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.Shop;

/// <summary>
/// One picture of one article, for the public shop.
///
/// Separate from <c>Artikujt.PhotoPath</c> (and the never-used <c>Artikujt.Foto</c> blob)
/// because a shop listing needs several angles of the same item, and because the BMD
/// columns belong to the accounting import — the ETL rewrites that table and would
/// happily wipe anything we wrote into it.
///
/// <see cref="Url"/> is a path under the media root (<c>/media/…</c>), never a remote
/// address: a photo pasted in from a supplier's site is DOWNLOADED and re-served from
/// here. Hotlinking a Chinese marketplace's CDN would put the shop's product images at
/// the mercy of a host that rate-limits, hotlink-blocks, or simply deletes the file the
/// week after the listing goes up.
/// </summary>
[Table("ArticlePhotos")]
public class ArticlePhoto
{
    /// <summary>How many pictures one article may carry. The storefront shows a main
    /// image plus thumbnails; past five, nobody is looking.</summary>
    public const int MaxPerArticle = 5;

    public int Id { get; set; }

    /// <summary>FK to <c>Artikujt.id</c>, which is a bigint.</summary>
    public long ArticleId { get; set; }

    [Required, StringLength(500)]
    public string Url { get; set; } = "";

    /// <summary>0 is the main image — the one the grid and the cart show.</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Where the picture came from: the supplier URL it was downloaded from, or the
    /// filename it was uploaded as. Kept because in six months, when a customer says the
    /// item is not what the photo showed, "which picture is this and who put it there"
    /// is the first question and nothing else in the row can answer it.
    /// </summary>
    [StringLength(1000)]
    public string? Source { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
