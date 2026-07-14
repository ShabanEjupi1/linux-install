using System.Globalization;

namespace KosovaPOS.Core.Services;

/// <summary>
/// The wire format of the shop's basket cookie, and the rules for reading one safely.
///
/// It stores ARTICLE IDS AND QUANTITIES ONLY — no prices, no names, no totals. That is the
/// single most important property of the shop: a cookie is a value the customer can rewrite,
/// so a basket that carried its own prices would be a shop that sells for whatever the buyer
/// says it costs. Everything a customer is charged is looked up from the database at
/// checkout by <see cref="ShopService.PlaceOrderAsync"/>.
///
/// Lives in Core rather than beside the cookie plumbing in the web layer so it can be tested
/// without an HttpContext — the parsing is where the hostile input arrives.
/// </summary>
public static class CartCookie
{
    public const string Name = "enisi_cart";

    /// <summary>An upper bound on one line's quantity. It is attacker-supplied and it gets
    /// multiplied by a price; stock is checked at checkout anyway, so this only keeps the
    /// arithmetic sane long before it gets there.</summary>
    public const decimal MaxQuantity = 999m;

    /// <summary>
    /// "12:2,88:1" — article id, colon, quantity. Compact enough to stay far inside the 4 KB
    /// a cookie gets.
    ///
    /// Anything malformed is dropped rather than throwing, and a hand-edited cookie will be
    /// malformed: a corrupt basket must produce an empty basket, not a 500 on the shop's
    /// front page.
    /// </summary>
    public static List<CartLine> Parse(string? raw)
    {
        var lines = new List<CartLine>();
        if (string.IsNullOrWhiteSpace(raw)) return lines;

        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var bits = part.Split(':');
            if (bits.Length != 2) continue;
            if (!long.TryParse(bits[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)) continue;
            if (!decimal.TryParse(bits[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var qty)) continue;

            // A negative quantity would become a negative charge; a negative id names nothing.
            if (id <= 0 || qty <= 0) continue;

            lines.Add(new CartLine(id, Math.Min(qty, MaxQuantity)));
        }

        return lines;
    }

    public static string Serialise(IEnumerable<CartLine> lines)
        => string.Join(",", lines.Select(l =>
            $"{l.ArticleId}:{l.Quantity.ToString(CultureInfo.InvariantCulture)}"));
}
