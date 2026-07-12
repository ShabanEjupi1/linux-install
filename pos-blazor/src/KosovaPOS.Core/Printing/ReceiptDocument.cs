using System.Globalization;
using System.Text;
using KosovaPOS.Core.Services;

namespace KosovaPOS.Core.Printing;

/// <summary>How a line is stressed. Maps to a CSS class in the browser and to ESC/POS bytes in the agent.</summary>
public enum ReceiptEmphasis
{
    Normal,
    /// <summary>Bold (ESC E 1). The shop name, the total.</summary>
    Bold,
    /// <summary>Double-height (GS ! 0x01). Reserved for the one headline line.</summary>
    Title,
}

/// <param name="Text">Already padded to the document's column width. Never re-wrap it.</param>
public readonly record struct ReceiptTextLine(string Text, ReceiptEmphasis Emphasis = ReceiptEmphasis.Normal, bool Center = false);

/// <summary>The shop's header block. Kept separate from BusinessSettings so Core printing has no EF dependency.</summary>
public sealed record ReceiptHeader(
    string Name,
    string? Address = null,
    string? Phone = null,
    string? FiscalNumber = null,
    string? VatNumber = null);

/// <summary>
/// Lays a sale out as fixed-width monospace lines — the same thing the desktop did with
/// Courier New through GDI, and the reason its receipts came out right on the Epson.
///
/// <b>Meant to become the single source of the receipt layout.</b> The browser page renders
/// these lines into a &lt;pre&gt;. The hardware agent's ESC/POS driver still lays the receipt
/// out on its own and has NOT been moved onto this yet — it takes a different input shape
/// (Receipt, not Invoice) and no shop can run it today anyway. Move it when the agent is
/// actually installed somewhere; until then the two can still drift, which is the whole
/// problem this class exists to end.
///
/// Column width is the contract with the paper, not a style choice: an 80mm roll on an
/// Epson TM-T prints 72mm wide, which is 42 characters of Courier at this size (and 48 at
/// the printer's small font). Every amount is right-aligned into that grid, so the decimal
/// points line up down the receipt exactly as they did on the desktop.
/// </summary>
public static class ReceiptFormatter
{
    /// <summary>Characters across the printable width. 42 = 80mm roll (72mm printable) in Courier.</summary>
    public const int DefaultWidth = 42;

    private static readonly CultureInfo Money = CultureInfo.InvariantCulture;

    public static List<ReceiptTextLine> Format(Invoice inv, ReceiptHeader shop, int width = DefaultWidth)
    {
        ArgumentNullException.ThrowIfNull(inv);
        ArgumentNullException.ThrowIfNull(shop);
        if (width < 24) throw new ArgumentOutOfRangeException(nameof(width), "A receipt narrower than 24 columns cannot hold an amount column.");

        var o = new List<ReceiptTextLine>();

        // ── header ──────────────────────────────────────────────────────
        o.Add(new ReceiptTextLine(Center(shop.Name.ToUpperInvariant(), width), ReceiptEmphasis.Title, Center: true));
        foreach (var extra in new[] { shop.Address, Prefixed("Tel: ", shop.Phone),
                                      Prefixed("Nr. fiskal: ", shop.FiscalNumber),
                                      Prefixed("Nr. TVSH: ", shop.VatNumber) })
        {
            if (!string.IsNullOrWhiteSpace(extra))
                o.Add(new ReceiptTextLine(Center(extra!, width), Center: true));
        }

        o.Add(Rule('-', width));
        o.Add(new ReceiptTextLine(Pair("Kuponi:", inv.Number, width)));
        o.Add(new ReceiptTextLine(Pair("Data:", $"{inv.Date:dd.MM.yyyy} {inv.Time}", width)));
        o.Add(new ReceiptTextLine(Pair("Arkëtari:", Clip(inv.CashierName, width - 12), width)));
        o.Add(Rule('-', width));

        // ── items ───────────────────────────────────────────────────────
        // Name on its own line (wrapped, never truncated — a cashier checking a receipt
        // against the shelf needs the whole name), then the qty × price = total line the
        // desktop printed, with the amount right-aligned into the grid.
        foreach (var l in inv.Lines)
        {
            foreach (var chunk in Wrap(l.Name, width))
                o.Add(new ReceiptTextLine(chunk.PadRight(width)));

            var qty = $"  {Num(l.Quantity, 3)} x {Num(l.UnitPriceGross, 2)}";
            o.Add(new ReceiptTextLine(Pair(qty, Num(l.GrossValue, 2), width)));

            if (l.DiscountPercent > 0)
                o.Add(new ReceiptTextLine(Pair($"  Zbritje {Num(l.DiscountPercent, 1)}%", "", width)));
        }

        // ── totals ──────────────────────────────────────────────────────
        o.Add(Rule('-', width));
        o.Add(new ReceiptTextLine(Pair("Pa TVSH:", Money2(inv.TotalNet), width)));

        foreach (var v in inv.VatSummary)
            o.Add(new ReceiptTextLine(Pair($"TVSH {Num(v.Rate, 0)}%:", Money2(v.Vat), width)));

        o.Add(new ReceiptTextLine(Pair("TOTALI:", Money2(inv.TotalGross), width), ReceiptEmphasis.Bold));
        o.Add(new ReceiptTextLine(Pair("Pagesa:", Clip(inv.PaymentMethod, width - 10), width)));
        o.Add(Rule('=', width));

        // ── footer ──────────────────────────────────────────────────────
        o.Add(new ReceiptTextLine(Center("Faleminderit për blerjen!", width), Center: true));

        return o;
    }

    /// <summary>The document as plain text — what the agent sends, and what a test asserts on.</summary>
    public static string ToText(IEnumerable<ReceiptTextLine> lines)
    {
        var sb = new StringBuilder();
        foreach (var l in lines)
            sb.Append(l.Text.TrimEnd()).Append('\n');
        return sb.ToString();
    }

    private static ReceiptTextLine Rule(char c, int width) => new(new string(c, width));

    private static string? Prefixed(string prefix, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : prefix + value;

    /// <summary>
    /// A label on the left and a value hard against the right edge. This — not a flexbox
    /// row — is what makes the decimal points line up on paper.
    /// </summary>
    private static string Pair(string left, string right, int width)
    {
        if (left.Length + right.Length + 1 > width)
            left = Clip(left, Math.Max(1, width - right.Length - 1));

        return left + new string(' ', Math.Max(1, width - left.Length - right.Length)) + right;
    }

    private static string Center(string s, int width)
    {
        s = Clip(s, width);
        var pad = (width - s.Length) / 2;
        return new string(' ', pad) + s;
    }

    /// <summary>Breaks a long article name on word boundaries, falling back to a hard cut for one long token.</summary>
    private static IEnumerable<string> Wrap(string s, int width)
    {
        s = (s ?? "").Trim();
        if (s.Length == 0) return [""];
        if (s.Length <= width) return [s];

        var lines = new List<string>();
        var line = new StringBuilder();

        foreach (var word in s.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var w = word;
            while (w.Length > width)          // a single token longer than the paper
            {
                if (line.Length > 0) { lines.Add(line.ToString()); line.Clear(); }
                lines.Add(w[..width]);
                w = w[width..];
            }

            if (line.Length == 0) line.Append(w);
            else if (line.Length + 1 + w.Length <= width) line.Append(' ').Append(w);
            else { lines.Add(line.ToString()); line.Clear(); line.Append(w); }
        }

        if (line.Length > 0) lines.Add(line.ToString());
        return lines;
    }

    private static string Clip(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s ?? "" : s[..Math.Max(0, max)];

    private static string Num(decimal d, int decimals) =>
        d.ToString("F" + decimals, Money);

    private static string Money2(decimal d) => Num(d, 2) + " EUR";
}
