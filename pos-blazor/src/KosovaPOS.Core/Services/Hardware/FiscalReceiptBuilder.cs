using System.Globalization;
using System.Text;
using KosovaPOS.Models;

namespace KosovaPOS.Core.Services.Hardware;

/// <summary>
/// Builds the F-Link INP payload for a fiscal receipt — the exact text the
/// desktop <c>FiscalPrinterService.GenerateFiscalReceipt</c> wrote to
/// <c>Fatura.inp</c>, but as a pure string with NO file I/O and NO logging.
///
/// Split out so the fiscal-format logic lives server-side (testable on any OS)
/// while the <c>KosovaPOS.Agent</c> on the cashier PC only drops the payload
/// into F-Link's watched folder. F-Link then talks COM to the fiscal device.
///
/// Format (inline-article mode, no PLU programming):
///   S,1,______,_,__;Name;UnitPrice;Qty;TaxGrp;Dept;PayType;0;PLU;0;0
///   Q,1,______,_,__;1;Pagoi: {paid}
///   Q,1,______,_,__;2;Kusur: {change}
///   T,1,______,_,__;
/// </summary>
public static class FiscalReceiptBuilder
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// F-Link's INP lines MUST end in CRLF. The desktop app wrote the file with
    /// <c>StringBuilder.AppendLine</c>, which on its Windows host emitted <c>\r\n</c> — and
    /// that is the only ending F-Link has ever parsed. This builder now runs server-side on
    /// Linux, where <c>Environment.NewLine</c> (and therefore <c>AppendLine</c>) is <c>\n</c>,
    /// so the ending is hard-coded here: a LF-only <c>Fatura.inp</c> lands in the watched
    /// folder but F-Link silently never processes it (file appears, nothing prints).
    /// </summary>
    private const string Crlf = "\r\n";

    /// <summary>Builds the INP payload. Throws <see cref="InvalidOperationException"/> on invalid items.</summary>
    public static string Build(Receipt receipt)
    {
        ValidateReceiptItems(receipt);

        var sb = new StringBuilder();
        foreach (var item in receipt.Items)
        {
            var taxGroup = GetTaxGroup(item.VATRate);
            const int dept = 1;    // Department 1
            const int payType = 3; // Cash payment type
            var unitPriceStr = item.Price.ToString("F2", Inv);
            var qtyStr = item.Quantity.ToString("F2", Inv);
            var name = SanitizeArticleName(item.ArticleName);
            var plu = item.PLU > 0 ? item.PLU : 0;
            sb.Append($"S,1,______,_,__;{name};{unitPriceStr};{qtyStr};{taxGroup};{dept};{payType};0;{plu};0;0{Crlf}");
        }

        var paidStr = receipt.PaidAmount.ToString("F2", Inv);
        sb.Append($"Q,1,______,_,__;1;Pagoi: {paidStr}{Crlf}");

        var change = receipt.PaidAmount - receipt.TotalAmount;
        if (change < 0) change = 0; // guard against partial payments
        sb.Append($"Q,1,______,_,__;2;Kusur: {change.ToString("F2", Inv)}{Crlf}");

        sb.Append($"T,1,______,_,__;{Crlf}");
        return sb.ToString();
    }

    /// <summary>Kosovo VAT groups: 1=18% (standard), 2=8% (reduced), 3=0% (zero-rated).</summary>
    public static int GetTaxGroup(decimal vatRate) => vatRate switch
    {
        18 => 1,
        8 => 2,
        _ => 3
    };

    /// <summary>Strips F-Link protocol delimiters; keeps a safe character set, caps at 250 chars.</summary>
    public static string SanitizeArticleName(string? articleName)
    {
        if (string.IsNullOrWhiteSpace(articleName))
            return "UNKNOWN_ARTICLE";

        var sb = new StringBuilder(articleName.Length);
        foreach (var ch in articleName)
        {
            if (char.IsLetterOrDigit(ch) || ch is ' ' or '-' or '.' or ',' or '&' or '/' or '(' or ')')
                sb.Append(ch);
            else
                sb.Append(' ');
        }

        var result = sb.ToString().Trim();
        if (result.Length > 250) result = result.Substring(0, 250);
        return string.IsNullOrWhiteSpace(result) ? "UNNAMED_ARTICLE" : result;
    }

    private static void ValidateReceiptItems(Receipt receipt)
    {
        if (receipt.Items is null || receipt.Items.Count == 0)
            throw new InvalidOperationException("Cannot create fiscal receipt — no items in receipt");

        var problems = new List<string>();
        foreach (var item in receipt.Items)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(item.ArticleName)) errors.Add("Missing article name");
            if (item.Quantity <= 0) errors.Add($"Invalid quantity: {item.Quantity}");
            if (item.Price <= 0) errors.Add($"Invalid price: {item.Price}");
            if (errors.Count > 0)
                problems.Add($"{(string.IsNullOrEmpty(item.ArticleName) ? "<no name>" : item.ArticleName)}: {string.Join(", ", errors)}");
        }

        if (problems.Count > 0)
            throw new InvalidOperationException("Cannot create fiscal receipt:\n" + string.Join("\n", problems));
    }
}
