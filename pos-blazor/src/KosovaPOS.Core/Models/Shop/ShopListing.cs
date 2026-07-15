using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.Shop;

/// <summary>
/// What the customer is told an article IS: a real name, and optionally a sentence about it.
///
/// This exists because <c>Artikujt.Emertimi</c> cannot be used and cannot be fixed. The
/// shop's article names are an accountant's names — a generic noun and an internal code,
/// "Loder 0115012" (<i>Toy 0115012</i>) — and nobody has ever bought a toy called
/// "Toy 0115012".
///
/// The obvious move is to retype the name in the POS. That is a trap. <c>Artikujt</c> is a
/// LOOKUP table for the BMD import (<c>tools/import-bmddata.py</c>), which upserts it with
/// <c>ON CONFLICT (id) DO UPDATE SET</c> <b>every column</b> from the shop PC's export. So a
/// name typed into the POS survives exactly until the next import and is then silently
/// reverted — and the loss is invisible, because the row is still there and still looks
/// fine. Retitling 74 articles by hand and having the work quietly deleted a week later is
/// the failure this table is here to prevent. Same reasoning as <see cref="ArticlePhoto"/>,
/// which stays out of <c>Artikujt.PhotoPath</c> for exactly this reason.
///
/// So: the till, the receipts, the VAT books and the accountant keep <c>Emertimi</c>. The
/// website reads this. Nothing here is ever exported back.
/// </summary>
[Table("ShopListings")]
public class ShopListing
{
    /// <summary>
    /// FK to <c>Artikujt.id</c>, and the primary key: one listing per article. Not an
    /// identity of its own, so a listing cannot be orphaned or duplicated.
    /// </summary>
    [Key]
    public long ArticleId { get; set; }

    /// <summary>
    /// The name a customer sees, e.g. "Kamion druri, i kuq". Null or blank means nobody has
    /// named this article yet, and the shop will not list it — see <c>ShopService</c>.
    /// </summary>
    [StringLength(200)]
    public string? Title { get; set; }

    /// <summary>A sentence or two on the product page. Optional; most items need none.</summary>
    [StringLength(2000)]
    public string? Description { get; set; }

    /// <summary>Who last wrote this, so a wrong name on the website has an author.</summary>
    [StringLength(100)]
    public string? UpdatedBy { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>True when this listing carries a usable name.</summary>
    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);
}
