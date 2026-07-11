using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Models;

namespace KosovaPOS.Core.Services;

/// <summary>
/// Builds the two official Kosovo ATK VAT-declaration books as .xlsx, laid out to
/// match the ministry templates (<c>Libri i Blerjeve</c> / <c>Libri i shitjeve</c>):
/// a three-row header — section title, column name, and the numbered box the value
/// feeds in the VAT declaration ([9], [12], [31], [43], …) — then one row per invoice.
///
/// Two deliberately different VAT treatments:
///  • <b>Sales (output VAT)</b> is carved out of the gross shelf price at the article's
///    resolved rate (Phase 13: <c>CShitjes</c> is VAT-inclusive, VAT is split out, never
///    added on top). This is correct even for historical rows that stored a zero VAT value.
///  • <b>Purchases (input/deductible VAT)</b> uses the VAT value recorded on the supplier
///    invoice (<c>Tvsh_Vl</c>) — deductible input VAT must equal the supplier document to
///    the cent and is never recomputed.
///
/// One PosDbContext, resolved to the caller's business by the tenant factory.
/// </summary>
public class VatBookService
{
    private readonly IDbContextFactory<PosDbContext> _dbFactory;

    public VatBookService(IDbContextFactory<PosDbContext> dbFactory) => _dbFactory = dbFactory;

    /// <summary>
    /// How many invoices each book would contain for the period — so the UI can warn
    /// before it hands the user an empty spreadsheet. <c>MaxDate</c> is the latest
    /// document date in each journal, which explains an empty period at a glance
    /// (e.g. "no purchases after March" when the period is July).
    /// </summary>
    public async Task<BookCounts> CountAsync(DateTime from, DateTime to)
    {
        var start = from.Date;
        var end = to.Date.AddDays(1);
        await using var db = await _dbFactory.CreateDbContextAsync();
        return new BookCounts
        {
            SalesInvoices = await db.DitariD.AsNoTracking()
                .Where(d => d.Data >= start && d.Data < end).Select(d => d.Numri).Distinct().CountAsync(),
            PurchaseInvoices = await db.DitariH.AsNoTracking()
                .Where(d => d.Data >= start && d.Data < end).Select(d => d.Numri).Distinct().CountAsync(),
            SalesMaxDate = await db.DitariD.AsNoTracking().MaxAsync(d => (DateTime?)d.Data),
            PurchaseMaxDate = await db.DitariH.AsNoTracking().MaxAsync(d => (DateTime?)d.Data),
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  SALES BOOK — Libri i shitjeve
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<byte[]> BuildSalesBookAsync(DateTime from, DateTime to)
    {
        var start = from.Date;
        var end = to.Date.AddDays(1);

        await using var db = await _dbFactory.CreateDbContextAsync();
        var lines = await db.DitariD.AsNoTracking()
            .Where(d => d.Data.HasValue && d.Data.Value >= start && d.Data.Value < end)
            .Select(d => new { d.Numri, d.Kuponi, d.Data, d.Subjekti, d.NrFiskalKlient, d.VleraMeTvsh, d.Vat, d.Tatimi })
            .ToListAsync();

        // Buyer names for invoices that name a customer (Subjekti → FurnitoriNew).
        var names = await LoadPartnerNamesAsync(db);

        // One row per fiscal invoice (receipt number). Aggregate its lines into VAT bands.
        var invoices = new Dictionary<long, SalesRow>();
        var order = new List<long>();
        foreach (var l in lines)
        {
            var key = l.Numri ?? 0;
            if (!invoices.TryGetValue(key, out var row))
            {
                row = new SalesRow
                {
                    Date = l.Data ?? start,
                    InvoiceNo = !string.IsNullOrWhiteSpace(l.Kuponi) ? l.Kuponi! : (l.Numri?.ToString() ?? ""),
                    BuyerName = l.Subjekti is int sid && names.TryGetValue(sid, out var n) ? n : "",
                    BuyerFiscalNo = l.NrFiskalKlient ?? "",
                };
                invoices[key] = row;
                order.Add(key);
            }
            else if (l.Data.HasValue && l.Data.Value < row.Date)
            {
                row.Date = l.Data.Value;
            }

            decimal gross = (decimal)(l.VleraMeTvsh ?? 0);
            if (gross == 0) continue;
            decimal rate = KosovoVat.Resolve((int?)l.Vat, l.Tatimi);
            decimal vat = KosovoVat.VatOf(gross, rate);
            decimal net = gross - vat;

            if (rate >= KosovoVat.Standard) { row.Net18 += net; row.Vat18 += vat; }
            else if (rate > 0m) { row.Net8 += net; row.Vat8 += vat; }
            else { row.Exempt += net; }
        }

        var rows = order.Select(k => invoices[k]).OrderBy(r => r.Date).ToList();
        return RenderSalesWorkbook(rows, start, to.Date);
    }

    private static byte[] RenderSalesWorkbook(List<SalesRow> rows, DateTime from, DateTime to)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Libri i shitjeve");

        // ── Row 1: section titles (merged) ──
        ws.Cell(1, 1).Value = "Nr."; ws.Range(1, 1, 2, 1).Merge();                  // A1:A2 (vertical)
        Section(ws, 1, 2, "Fatura", 2, 3);                                          // B1:C1
        Section(ws, 1, 4, "Blerësi", 4, 6);                                         // D1:F1
        Section(ws, 1, 7, "Shitjet e liruara nga TVSH", 7, 12);                     // G1:L1
        Section(ws, 1, 13, "Shitjet e tatueshme me normën 18%", 13, 18);           // M1:R1
        Section(ws, 1, 19, "Shitjet e tatueshme me normën 8%", 19, 23);            // S1:W1
        Section(ws, 1, 24, "Totali i TVSH-së së llogaritur me 18% dhe 8%", 24, 24); // X1:X2 (vertical)
        ws.Range(1, 24, 2, 24).Merge();

        // ── Row 2: column names ──
        string[] r2 =
        {
            /*A*/"", /*B*/"Data", /*C*/"Numri i faturës",
            /*D*/"Emri i blerësit", /*E*/"Numri Fiskal i blerësit", /*F*/"Numri i TVSH-së së blerësit",
            /*G*/"Shitjet e liruara pa të drejtë kreditimi", /*H*/"Shitjet e shërbimeve jashtë vendit",
            /*I*/"Shitjet brenda vendit me ngarkesë të kundërt të TVSH-së", /*J*/"Shitjet tjera të liruara me të drejtë kreditimi",
            /*K*/"Totali i shitjeve të liruara me të drejtë kreditimi", /*L*/"Eksportet",
            /*M*/"Shitjet e tatueshme", /*N*/"Nota debitore e lëshuar, nota kreditore e pranuar",
            /*O*/"Fatura e borxhit të keq e pranuar", /*P*/"Rregullimet për të rritur TVSH-në",
            /*Q*/"Blerjet që i nënshtrohen ngarkesës së kundërt", /*R*/"Totali i TVSH-së së llogaritur me normën 18%",
            /*S*/"Shitjet e tatueshme", /*T*/"Nota debitore e lëshuar, nota kreditore e pranuar",
            /*U*/"Fatura e borxhit të keq e pranuar", /*V*/"Rregullimet për të rritur TVSH-në",
            /*W*/"Totali i TVSH-së së llogaritur me normën 8%",
        };
        for (int i = 0; i < r2.Length; i++) if (r2[i].Length > 0) ws.Cell(2, i + 1).Value = r2[i];

        // ── Row 3: declaration box numbers ──
        ws.Cell(3, 1).Value = "Numri i kutisë në Deklaratën e TVSH-së";
        string[] boxes = { "[9]", "[10a]", "[10b]", "[10c]", "[10] = [10a]+[10b]+[10c]", "[11]",
                           "[12]", "[16]", "[20]", "[24]", "[28]", "[K1]",
                           "[14]", "[18]", "[22]", "[26]", "[K2]", "[30]" };
        for (int i = 0; i < boxes.Length; i++) ws.Cell(3, 7 + i).Value = boxes[i];

        StyleHeader(ws, 3, 24);

        // ── Data ──
        int row = 4;
        var totals = new decimal[25];
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = row - 3;
            ws.Cell(row, 2).Value = r.Date.ToString("dd.MM.yyyy");
            ws.Cell(row, 3).Value = r.InvoiceNo;
            ws.Cell(row, 4).Value = r.BuyerName;
            ws.Cell(row, 5).Value = r.BuyerFiscalNo;
            PutMoney(ws, row, 7, r.Exempt, totals);   // G  [9]
            PutMoney(ws, row, 13, r.Net18, totals);   // M  [12]
            PutMoney(ws, row, 18, r.Vat18, totals);   // R  [K1]
            PutMoney(ws, row, 19, r.Net8, totals);    // S  [14]
            PutMoney(ws, row, 23, r.Vat8, totals);    // W  [K2]
            PutMoney(ws, row, 24, r.Vat18 + r.Vat8, totals); // X [30]
            row++;
        }
        WriteTotals(ws, row, 6, totals, 24);
        Finish(ws, from, to, 24);
        return Save(wb);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PURCHASE BOOK — Libri i Blerjeve
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<byte[]> BuildPurchaseBookAsync(DateTime from, DateTime to)
    {
        var start = from.Date;
        var end = to.Date.AddDays(1);

        await using var db = await _dbFactory.CreateDbContextAsync();
        var lines = await db.DitariH.AsNoTracking()
            .Where(d => d.Data.HasValue && d.Data.Value >= start && d.Data.Value < end)
            .Select(d => new { d.Numri, d.Data, d.NrFatures, d.Tipi, d.Subjekti,
                               d.VleraMeTvsh, d.VleraFurn, d.TvshVl, d.TvshPer })
            .ToListAsync();

        var suppliers = await db.FurnitoriNew.AsNoTracking()
            .Select(f => new { f.Id, f.Emri, f.NRF, f.NIT })
            .ToListAsync();
        var byId = suppliers.ToDictionary(s => s.Id, s => s);

        var docs = new Dictionary<long, PurchaseRow>();
        var order = new List<long>();
        foreach (var l in lines)
        {
            var key = l.Numri ?? 0;
            if (!docs.TryGetValue(key, out var row))
            {
                byId.TryGetValue(l.Subjekti ?? -1, out var sup);
                row = new PurchaseRow
                {
                    Date = l.Data ?? start,
                    InvoiceNo = string.IsNullOrWhiteSpace(l.NrFatures) ? (l.Numri?.ToString() ?? "") : l.NrFatures,
                    SupplierName = sup?.Emri ?? "",
                    SupplierFiscalNo = sup?.NRF ?? "",
                    SupplierVatNo = sup?.NIT ?? "",
                    IsImport = (l.Tipi ?? "").Contains("import", StringComparison.OrdinalIgnoreCase),
                };
                docs[key] = row;
                order.Add(key);
            }
            else if (l.Data.HasValue && l.Data.Value < row.Date)
            {
                row.Date = l.Data.Value;
            }

            decimal vat = (decimal)(l.TvshVl ?? 0);
            decimal gross = (decimal)(l.VleraMeTvsh ?? 0);
            decimal net = (decimal)(l.VleraFurn ?? 0);
            if (net == 0m) net = gross - vat;                 // fall back if net not stored
            decimal ratePct = (decimal)(l.TvshPer ?? 0);

            if (ratePct >= 14m || (vat > 0m && net > 0m && vat / net * 100m >= 14m))
            { row.Net18 += net; row.Vat18 += vat; }
            else if (ratePct > 0m || vat > 0m)
            { row.Net8 += net; row.Vat8 += vat; }
            else
            { row.Exempt += net; }
        }

        var rows = order.Select(k => docs[k]).OrderBy(r => r.Date).ToList();
        return RenderPurchaseWorkbook(rows, start, to.Date);
    }

    private static byte[] RenderPurchaseWorkbook(List<PurchaseRow> rows, DateTime from, DateTime to)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Libri i Blerjeve");

        // ── Row 1: section titles ──
        ws.Cell(1, 1).Value = "Nr."; ws.Range(1, 1, 2, 1).Merge();                  // A1:A2 (vertical)
        Section(ws, 1, 2, "Fatura", 2, 3);                                          // B1:C1
        Section(ws, 1, 4, "Shitësi", 4, 6);                                         // D1:F1
        Section(ws, 1, 7, "Blerjet dhe Importet e liruara dhe me TVSH jo të zbritshme", 7, 10);  // G1:J1
        Section(ws, 1, 11, "Blerjet dhe Importet e tatushme me 18%, si dhe rregullimet e zbritjeve", 11, 19); // K1:S1
        Section(ws, 1, 20, "Blerjet dhe Importet e tatushme me 8%, si dhe rregullimet e zbritjeve", 20, 28);  // T1:AB1
        Section(ws, 1, 29, "Totali i TVSH-së së zbritshme me 18% dhe 8%", 29, 29);   // AC1:AC2
        ws.Range(1, 29, 2, 29).Merge();

        // ── Row 2: column names ──
        string[] r2 =
        {
            /*A*/"", /*B*/"Data", /*C*/"Numri i faturës",
            /*D*/"Emri i shitësit", /*E*/"Numri Fiskal i shitësit", /*F*/"Numri i TVSH-së së shitësit",
            /*G*/"Blerjet dhe importet pa TVSH", /*H*/"Blerjet dhe importet investive pa TVSH",
            /*I*/"Blerjet dhe importet me TVSH jo të zbritshme", /*J*/"Blerjet dhe importet investive me TVSH jo të zbritshme",
            /*K*/"Importet", /*L*/"Importet investive", /*M*/"Blerjet vendore", /*N*/"Blerjet investive vendore",
            /*O*/"Nota debitore e pranuar, nota kreditore e lëshuar", /*P*/"Fatura e borxhit të keq e lëshuar",
            /*Q*/"Rregullimet për të ulur TVSH-në për pagesë", /*R*/"E drejta e kreditimit të TVSH-së në lidhje me Ngarkesën e Kundërt",
            /*S*/"Totali i TVSH-së së zbritshme me 18%",
            /*T*/"Importet", /*U*/"Importet investive", /*V*/"Blerjet vendore", /*W*/"Blerjet investive vendore",
            /*X*/"Blerjet nga fermerët (aplikimi i normës së sheshtë)", /*Y*/"Nota debitore e pranuar, nota kreditore e lëshuar",
            /*Z*/"Fatura e borxhit të keq e lëshuar", /*AA*/"Rregullimet për të ulur TVSH-në për pagesë",
            /*AB*/"Totali i TVSH-së së zbritshme me 8%",
        };
        for (int i = 0; i < r2.Length; i++) if (r2[i].Length > 0) ws.Cell(2, i + 1).Value = r2[i];

        // ── Row 3: declaration box numbers ──
        ws.Cell(3, 1).Value = "Numri i kutisë në Deklaratën e TVSH-së";
        string[] boxes = { "[31]", "[32]", "[33]", "[34]",
                           "[35]", "[39]", "[43]", "[47]", "[53]", "[57]", "[61]", "[65]", "[K1]",
                           "[37]", "[41]", "[45]", "[49]", "[51]", "[55]", "[59]", "[63]", "[K2]", "[67]" };
        for (int i = 0; i < boxes.Length; i++) ws.Cell(3, 7 + i).Value = boxes[i];

        StyleHeader(ws, 3, 29);

        // ── Data ──
        int row = 4;
        var totals = new decimal[30];
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = row - 3;
            ws.Cell(row, 2).Value = r.Date.ToString("dd.MM.yyyy");
            ws.Cell(row, 3).Value = r.InvoiceNo;
            ws.Cell(row, 4).Value = r.SupplierName;
            ws.Cell(row, 5).Value = r.SupplierFiscalNo;
            ws.Cell(row, 6).Value = r.SupplierVatNo;
            PutMoney(ws, row, 7, r.Exempt, totals);                          // G  [31] no-VAT
            PutMoney(ws, row, r.IsImport ? 11 : 13, r.Net18, totals);        // K [35] import / M [43] domestic
            PutMoney(ws, row, 19, r.Vat18, totals);                          // S  [K1]
            PutMoney(ws, row, r.IsImport ? 20 : 22, r.Net8, totals);         // T [37] import / V [45] domestic
            PutMoney(ws, row, 28, r.Vat8, totals);                           // AB [K2]
            PutMoney(ws, row, 29, r.Vat18 + r.Vat8, totals);                 // AC [67]
            row++;
        }
        WriteTotals(ws, row, 6, totals, 29);
        Finish(ws, from, to, 29);
        return Save(wb);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Shared rendering helpers
    // ─────────────────────────────────────────────────────────────────────────
    private static async Task<Dictionary<int, string>> LoadPartnerNamesAsync(PosDbContext db) =>
        (await db.FurnitoriNew.AsNoTracking().Select(f => new { f.Id, f.Emri }).ToListAsync())
        .ToDictionary(f => (int)f.Id, f => f.Emri ?? "");

    private static void Section(IXLWorksheet ws, int row, int col, string text, int fromCol, int toCol)
    {
        ws.Cell(row, col).Value = text;
        if (toCol > fromCol) ws.Range(row, fromCol, row, toCol).Merge();
    }

    private static void PutMoney(IXLWorksheet ws, int row, int col, decimal value, decimal[] totals)
    {
        if (value == 0m) return;
        var v = decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        var cell = ws.Cell(row, col);
        cell.Value = v;
        cell.Style.NumberFormat.Format = "#,##0.00";
        totals[col] += v;
    }

    private static void StyleHeader(IXLWorksheet ws, int lastHeaderRow, int lastCol)
    {
        var head = ws.Range(1, 1, lastHeaderRow, lastCol);
        head.Style.Font.Bold = true;
        head.Style.Alignment.WrapText = true;
        head.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        head.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        head.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF7");
        head.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        head.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        ws.SheetView.FreezeRows(lastHeaderRow);
        ws.Row(2).Height = 90;
    }

    private static void WriteTotals(IXLWorksheet ws, int row, int labelSpanTo, decimal[] totals, int lastCol)
    {
        ws.Cell(row, 1).Value = "TOTALI";
        ws.Range(row, 1, row, labelSpanTo).Merge();
        for (int c = labelSpanTo + 1; c <= lastCol; c++)
        {
            if (totals[c] == 0m) continue;
            var cell = ws.Cell(row, c);
            cell.Value = decimal.Round(totals[c], 2, MidpointRounding.AwayFromZero);
            cell.Style.NumberFormat.Format = "#,##0.00";
        }
        var range = ws.Range(row, 1, row, lastCol);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
        range.Style.Border.TopBorder = XLBorderStyleValues.Medium;
    }

    private static void Finish(IXLWorksheet ws, DateTime from, DateTime to, int lastCol)
    {
        ws.Columns(1, 1).Width = 5;
        ws.Columns(2, 3).Width = 15;
        ws.Columns(4, 4).Width = 26;
        ws.Columns(5, 6).Width = 16;
        ws.Columns(7, lastCol).Width = 13;
        ws.RangeUsed()?.SetAutoFilter(false);
        // A trailing note with the period the book covers — off to the side so it
        // never collides with the fixed ATK columns.
        var note = ws.Cell(1, lastCol + 2);
        note.Value = $"Periudha: {from:dd.MM.yyyy} – {to:dd.MM.yyyy}";
        note.Style.Font.Italic = true;
    }

    private static byte[] Save(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public sealed class BookCounts
    {
        public int SalesInvoices { get; set; }
        public int PurchaseInvoices { get; set; }
        public DateTime? SalesMaxDate { get; set; }
        public DateTime? PurchaseMaxDate { get; set; }
    }

    private sealed class SalesRow
    {
        public DateTime Date;
        public string InvoiceNo = "";
        public string BuyerName = "";
        public string BuyerFiscalNo = "";
        public decimal Net18, Vat18, Net8, Vat8, Exempt;
    }

    private sealed class PurchaseRow
    {
        public DateTime Date;
        public string InvoiceNo = "";
        public string SupplierName = "";
        public string SupplierFiscalNo = "";
        public string SupplierVatNo = "";
        public bool IsImport;
        public decimal Net18, Vat18, Net8, Vat8, Exempt;
    }
}
