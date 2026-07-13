using KosovaPOS.Agent.Contracts;
using KosovaPOS.Core.Services;
using KosovaPOS.Models;

namespace KosovaPOS.Core.Printing;

/// <summary>
/// What the cashier typed into the buyer block of an A4 document before printing it. A walk-in
/// sale records no buyer, so on the invoice these are blank boxes on the screen — and a delivery
/// note whose receiver is nobody proves nothing about where the goods went, which is the only
/// reason it exists. Whatever is typed there travels with the print job.
/// </summary>
public sealed record A4Party(
    string? Name = null,
    string? FiscalNumber = null,
    string? Address = null,
    string? Vehicle = null,
    string? LoadedAt = null,
    string? Departure = null);

/// <summary>
/// Lays the A4 invoice and waybill out as content the agent can draw on paper.
///
/// The agent is a Windows service with no browser in it, so it cannot render <c>/fatura/{n}</c>.
/// And the browser, which can, cannot choose a printer — Chrome's kiosk-printing always uses the
/// Windows default, so an A4 invoice printed that way lands on the thermal roll whenever the till's
/// default is the receipt printer. So the A4 paper is laid out HERE, once, and handed to the agent
/// as a document: same numbers, same columns, same order as the page on screen.
/// </summary>
public static class A4DocumentBuilder
{
    private const string Euro = " €";

    public static A4Document Invoice(Invoice invoice, BusinessSettings? shop, A4Party? buyer = null)
    {
        var doc = new A4Document
        {
            DocumentTitle = "FATURË",
            SellerLines = SellerBlock(shop, includeEmail: true),
            Meta =
            {
                new A4Field { Key = "Numri:",     Value = invoice.Number, Emphasis = true },
                new A4Field { Key = "Data:",      Value = $"{invoice.Date:dd.MM.yyyy} {invoice.Time}" },
                new A4Field { Key = "Pagesa:",    Value = invoice.PaymentMethod },
                new A4Field { Key = "Arkëtari:",  Value = invoice.CashierName },
            },
            PartyTitle = "Blerësi",
            Columns =
            {
                Col("#", 0.5, Center), Col("Përshkrimi", 5), Col("Njësia", 1, Center),
                Col("Sasia", 1, Right), Col("Çmimi pa TVSH", 1.6, Right), Col("TVSH %", 1, Right),
                Col("Vlera pa TVSH", 1.5, Right), Col("TVSH", 1.3, Right), Col("Vlera me TVSH", 1.6, Right),
            },
            SummaryTitle = "Përmbledhja e TVSH-së",
            SummaryColumns = { Col("Norma", 1, Right), Col("Baza", 1, Right), Col("TVSH", 1, Right), Col("Totali", 1, Right) },
            Totals =
            {
                new A4Field { Key = "Total pa TVSH:",     Value = Money(invoice.TotalNet) },
                new A4Field { Key = "Total TVSH:",        Value = Money(invoice.TotalVat) },
                new A4Field { Key = "TOTALI PËR PAGESË:", Value = Money(invoice.TotalGross), Emphasis = true },
            },
            Signatures = { "Nënshkrimi i shitësit", "Nënshkrimi i blerësit" },
        };

        FillParty(doc, buyer, invoice, addressLabel: "Adresa");

        var n = 1;
        foreach (var l in invoice.Lines)
        {
            doc.Rows.Add(new A4Row
            {
                Cells =
                {
                    (n++).ToString(),
                    l.Name,
                    l.Unit,
                    l.Quantity.ToString("0.###"),
                    l.UnitPriceNet.ToString("0.0000") + Euro,
                    l.VatRate.ToString("0.##") + "%",
                    Money(l.NetValue),
                    Money(l.VatValue),
                    Money(l.GrossValue),
                },
            });
        }

        foreach (var v in invoice.VatSummary)
        {
            doc.SummaryRows.Add(new A4Row
            {
                Cells = { v.Rate.ToString("0.##") + "%", Money(v.Net), Money(v.Vat), Money(v.Gross) },
            });
        }

        if (!string.IsNullOrWhiteSpace(shop?.BankAccount))
            doc.Notes.Add($"Llogaria bankare: {shop!.BankAccount}");

        doc.Notes.Add($"Faturë e gjeneruar nga KosovaPOS · {SellerName(shop)}");

        return doc;
    }

    public static A4Document Waybill(Invoice invoice, BusinessSettings? shop, A4Party? receiver = null)
    {
        var doc = new A4Document
        {
            DocumentTitle = "FLETËDËRGESË",
            SellerLines = SellerBlock(shop, includeEmail: false),
            Meta =
            {
                new A4Field { Key = "Numri:",          Value = invoice.Number, Emphasis = true },
                new A4Field { Key = "Data:",           Value = $"{invoice.Date:dd.MM.yyyy} {invoice.Time}" },
                new A4Field { Key = "Sipas faturës:",  Value = "#" + invoice.Number },
            },
            PartyTitle = "Pranuesi",
            Columns =
            {
                Col("#", 0.5, Center), Col("Përshkrimi i mallit", 5), Col("Njësia", 1, Center),
                Col("Sasia", 1, Right), Col("Çmimi", 1.2, Right), Col("Vlera", 1.4, Right),
            },
            Totals =
            {
                new A4Field { Key = "Sasia gjithsej:", Value = invoice.Lines.Sum(l => l.Quantity).ToString("0.###") },
                new A4Field { Key = "TOTALI:",         Value = Money(invoice.TotalGross), Emphasis = true },
            },
            Signatures = { "Dërguesi (nënshkrimi)", "Transportuesi (nënshkrimi)", "Pranuesi (nënshkrimi)" },
        };

        FillParty(doc, receiver, invoice, addressLabel: "Adresa e dërgesës");

        // The transport block is only on the paper if somebody filled it in — three empty labels
        // print as three empty labels, and a driver cannot sign a lorry that is not named.
        if (!string.IsNullOrWhiteSpace(receiver?.Vehicle))
            doc.Party.Add(new A4Field { Key = "Automjeti:", Value = receiver!.Vehicle! });
        if (!string.IsNullOrWhiteSpace(receiver?.LoadedAt))
            doc.Party.Add(new A4Field { Key = "Vendi i ngarkimit:", Value = receiver!.LoadedAt! });
        if (!string.IsNullOrWhiteSpace(receiver?.Departure))
            doc.Party.Add(new A4Field { Key = "Ora e nisjes:", Value = receiver!.Departure! });

        var n = 1;
        foreach (var l in invoice.Lines)
        {
            doc.Rows.Add(new A4Row
            {
                Cells =
                {
                    (n++).ToString(),
                    l.Name,
                    l.Unit,
                    l.Quantity.ToString("0.###"),
                    Money(l.UnitPriceGross),
                    Money(l.GrossValue),
                },
            });
        }

        // A fletëdërgesë travels WITH the goods and is not itself the tax document. Saying so on
        // the paper is what stops it being handed over in place of the invoice.
        doc.Notes.Add(
            "Kjo fletëdërgesë shoqëron mallin gjatë transportit dhe nuk zëvendëson faturën tatimore. " +
            $"Fatura përkatëse: #{invoice.Number} e datës {invoice.Date:dd.MM.yyyy}.");

        return doc;
    }

    // ── shared blocks ────────────────────────────────────────────────────────

    private static void FillParty(A4Document doc, A4Party? party, Invoice invoice, string addressLabel)
    {
        // What the cashier typed wins over what the sale recorded; a walk-in recorded "Qytetar",
        // which is not a buyer and has no place on a tax invoice.
        var name    = First(party?.Name,         invoice.BuyerName is { } b && b != "Qytetar" ? b : null);
        var fiscal  = First(party?.FiscalNumber, invoice.BuyerFiscalNumber);
        var address = First(party?.Address,      invoice.BuyerAddress);

        if (name    is not null) doc.Party.Add(new A4Field { Key = "Emri:",           Value = name, Emphasis = true });
        if (fiscal  is not null) doc.Party.Add(new A4Field { Key = "Nr. fiskal/NUI:", Value = fiscal });
        if (address is not null) doc.Party.Add(new A4Field { Key = addressLabel + ":", Value = address });

        // Nothing known about the buyer at all: print the block with ruled blanks, so it can be
        // written in by hand rather than leaving a tax invoice with no buyer field on it.
        if (doc.Party.Count == 0)
        {
            doc.Party.Add(new A4Field { Key = "Emri:",           Value = "" });
            doc.Party.Add(new A4Field { Key = "Nr. fiskal/NUI:", Value = "" });
            doc.Party.Add(new A4Field { Key = addressLabel + ":", Value = "" });
        }
    }

    private static List<string> SellerBlock(BusinessSettings? shop, bool includeEmail)
    {
        var lines = new List<string> { SellerName(shop) };

        if (!string.IsNullOrWhiteSpace(shop?.Address)) lines.Add(shop!.Address!);

        var ids = new List<string>();
        if (!string.IsNullOrWhiteSpace(shop?.FiscalNumber)) ids.Add($"Nr. fiskal: {shop!.FiscalNumber}");
        if (!string.IsNullOrWhiteSpace(shop?.VatNumber))    ids.Add($"Nr. TVSH: {shop!.VatNumber}");
        if (ids.Count > 0) lines.Add(string.Join("   ", ids));

        var contact = new List<string>();
        if (!string.IsNullOrWhiteSpace(shop?.Phone)) contact.Add($"Tel: {shop!.Phone}");
        if (includeEmail && !string.IsNullOrWhiteSpace(shop?.Email)) contact.Add(shop!.Email!);
        if (contact.Count > 0) lines.Add(string.Join("   ", contact));

        return lines;
    }

    private static string SellerName(BusinessSettings? shop) =>
        string.IsNullOrWhiteSpace(shop?.BusinessName) ? "KosovaPOS" : shop!.BusinessName!;

    private const int Center = 1;
    private const int Right = 2;

    private static A4Column Col(string header, double weight, int align = 0) =>
        new() { Header = header, Weight = weight, Align = align };

    private static string Money(decimal v) => v.ToString("0.00") + Euro;

    private static string? First(params string?[] candidates) =>
        candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))?.Trim();
}
