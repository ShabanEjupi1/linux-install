using System.Text.Json;
using KosovaPOS.Models;

namespace KosovaPOS.Core.Printing;

/// <summary>
/// The close-of-shift slip ("raporti i ndërrimit"), laid out on the same fixed-width grid as
/// the receipt — so it comes off the same 80mm roll, through the same agent, with no dialog.
///
/// This is the paper a cashier signs and hands to the owner with the cash bag. It therefore
/// prints the numbers that a disagreement is settled with: what the till started with, what it
/// sold, what the app EXPECTED to be in the drawer, what was actually counted, and the
/// difference between the two — never a rounded or "corrected" figure. If the drawer is short,
/// the slip says it is short.
/// </summary>
public static class ShiftReportFormatter
{
    public static List<ReceiptTextLine> Format(
        CashShift shift, ReceiptHeader shop, int width = ReceiptFormatter.DefaultWidth)
    {
        ArgumentNullException.ThrowIfNull(shift);
        ArgumentNullException.ThrowIfNull(shop);

        var o = new List<ReceiptTextLine>
        {
            new(ReceiptFormatter.Center(shop.Name.ToUpperInvariant(), width), ReceiptEmphasis.Title, Center: true),
            new(ReceiptFormatter.Center("RAPORTI I NDËRRIMIT", width), ReceiptEmphasis.Bold, Center: true),
        };

        var open = shift.Status == ShiftStatus.Open;
        if (open)
            o.Add(new ReceiptTextLine(ReceiptFormatter.Center("(ndërrim i hapur — jo përfundimtar)", width), Center: true));

        o.Add(ReceiptFormatter.Rule('-', width));
        o.Add(Row("Ndërrimi:", $"#{shift.Id}", width));
        o.Add(Row("Arkëtari:", ReceiptFormatter.Clip(shift.CashierName ?? "", width - 12), width));
        o.Add(Row("Hapur:", shift.OpenedAt.ToString("dd.MM.yyyy HH:mm"), width));
        o.Add(Row("Mbyllur:", shift.ClosedAt?.ToString("dd.MM.yyyy HH:mm") ?? "—", width));
        o.Add(ReceiptFormatter.Rule('-', width));

        o.Add(Row("Para fillestare:", Money(shift.OpeningCash), width));
        o.Add(Row("Shitje me para:", Money(shift.TotalCashSales), width));
        o.Add(Row("Shitje me kartë:", Money(shift.TotalCardSales), width));
        o.Add(Row("Shitje gjithsej:", Money(shift.TotalSales), width, ReceiptEmphasis.Bold));
        o.Add(Row("Transaksione:", shift.TransactionCount.ToString(), width));
        o.Add(ReceiptFormatter.Rule('-', width));

        o.Add(Row("Pritej në arkë:", Money(shift.ExpectedCash), width));

        // An open shift has no counted cash and therefore no difference — the drawer has not been
        // counted yet. Printing "0.00 EUR" for both would read as "counted, and it balances",
        // which is a statement about money that nobody has made. It gets a dash.
        o.Add(Row("U numërua:", open ? "—" : Money(shift.ClosingCash), width));
        // The difference is the number an investigation starts from. Once it exists it is printed
        // with its sign, and it is printed even when it is ugly.
        o.Add(Row("DIFERENCA:", open ? "—" : Money(shift.CashDifference), width, ReceiptEmphasis.Bold));

        foreach (var line in Denominations(shift.DenominationCountJson, width))
            o.Add(line);

        if (!string.IsNullOrWhiteSpace(shift.Notes))
        {
            o.Add(ReceiptFormatter.Rule('-', width));
            o.Add(new ReceiptTextLine("Shënime:".PadRight(width)));
            foreach (var chunk in ReceiptFormatter.Wrap(shift.Notes!, width))
                o.Add(new ReceiptTextLine(chunk.PadRight(width)));
        }

        o.Add(ReceiptFormatter.Rule('=', width));
        o.Add(new ReceiptTextLine("Arkëtari: ______________________".PadRight(width)));
        o.Add(new ReceiptTextLine("Pranoi:   ______________________".PadRight(width)));

        return o;
    }

    /// <summary>The note/coin count as it was entered at close, if it was entered at all.</summary>
    private static IEnumerable<ReceiptTextLine> Denominations(string? json, int width)
    {
        if (string.IsNullOrWhiteSpace(json)) yield break;

        Dictionary<string, int>? counts;
        try { counts = JsonSerializer.Deserialize<Dictionary<string, int>>(json!); }
        catch (JsonException) { yield break; }   // a hand-edited row must not break the print
        if (counts is null || counts.Count == 0) yield break;

        yield return ReceiptFormatter.Rule('-', width);
        yield return new ReceiptTextLine("Numërimi i kartëmonedhave".PadRight(width));

        foreach (var (face, count) in counts
                     .Select(kv => (Face: decimal.TryParse(kv.Key,
                                        System.Globalization.NumberStyles.Any,
                                        System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 0m,
                                    Count: kv.Value))
                     .Where(x => x.Face > 0)
                     .OrderByDescending(x => x.Face))
        {
            yield return Row($"  {face:0.00} x {count}", Money(face * count), width);
        }
    }

    private static ReceiptTextLine Row(string left, string right, int width,
                                       ReceiptEmphasis emphasis = ReceiptEmphasis.Normal) =>
        new(ReceiptFormatter.Pair(left, right, width), emphasis);

    private static string Money(decimal d) => ReceiptFormatter.Money2(d);
}
