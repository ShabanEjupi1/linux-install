using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.Versioning;
using KosovaPOS.Agent.Contracts;

namespace KosovaPOS.Agent.Drivers.Windows;

/// <summary>
/// Prints the A4 paper — the invoice and the waybill — straight to a named Windows printer,
/// with no print dialog.
///
/// The browser cannot do this. A web page has no say in which printer it prints to: the user
/// picks one in the dialog, and Chrome's --kiosk-printing, which is what removes the dialog,
/// always prints to the WINDOWS DEFAULT printer. On a till whose default is the 80mm thermal
/// roll — which is the normal setup, because that is what the receipts come off — an A4 invoice
/// printed from the browser comes out of the thermal printer as a metre of curling paper. The
/// only way to say "this sheet goes to the office laser, that slip goes to the roll" is to be a
/// process on the PC that talks to the spooler, which is this.
///
/// It draws with GDI rather than rendering the POS's HTML, because a Windows service has no
/// browser in it. The document arrives already laid out by <c>A4DocumentBuilder</c> on the
/// server, so the paper carries the same numbers, columns and order as the page on screen.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsA4Driver : IA4Driver
{
    private readonly AgentConfig _cfg;
    private readonly ILogger<WindowsA4Driver> _log;

    public WindowsA4Driver(AgentConfig cfg, ILogger<WindowsA4Driver> log)
    {
        _cfg = cfg;
        _log = log;
    }

    public bool Available => true;

    public Task<AgentResult> PrintAsync(A4PrintRequest req, CancellationToken ct = default)
    {
        var printer = WindowsPrinters.Resolve(req.PrinterName, _cfg.InvoicePrinter);
        if (string.IsNullOrWhiteSpace(printer))
            return Task.FromResult(AgentResult.Fail(
                "Nuk është zgjedhur printeri A4 dhe ky kompjuter nuk ka printer të parazgjedhur."));

        // Printing blocks on the spooler; keep the agent's request thread free.
        return Task.Run(() =>
        {
            try
            {
                using var pd = new PrintDocument();
                pd.PrinterSettings.PrinterName = printer;

                // A mistyped printer name is otherwise silent — the job goes nowhere and the shop
                // is left looking at a printer that seems to be switched off.
                if (!pd.PrinterSettings.IsValid)
                    return AgentResult.Fail($"Printeri '{printer}' nuk ekziston në këtë kompjuter.");

                pd.DocumentName = string.IsNullOrWhiteSpace(req.Title) ? "KosovaPOS A4" : req.Title;
                pd.PrinterSettings.Copies = (short)Math.Clamp(req.Copies, 1, 20);
                pd.DefaultPageSettings.Margins = new Margins(50, 50, 50, 55); // hundredths of an inch

                // The A4 tray, when the printer has one. A printer whose default is Letter would
                // otherwise crop the foot of the page — and the signatures live there.
                foreach (PaperSize size in pd.PrinterSettings.PaperSizes)
                {
                    if (size.Kind == PaperKind.A4)
                    {
                        pd.DefaultPageSettings.PaperSize = size;
                        break;
                    }
                }

                var painter = new A4Painter(req.Document);
                pd.PrintPage += painter.PrintPage;
                pd.Print();

                _log.LogInformation("A4 '{Title}' ({Rows} rreshta) → printeri '{Printer}' x{Copies}",
                    pd.DocumentName, req.Document.Rows.Count, printer, req.Copies);

                return AgentResult.Success();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "A4 print failed on printer '{Printer}'", printer);
                return AgentResult.Fail($"Printimi A4 dështoi ({printer}): {ex.Message}");
            }
        }, ct);
    }
}

/// <summary>
/// Draws an <see cref="A4Document"/> onto printer pages. Stateful across pages: it remembers how
/// far down the line table it got, so a 300-line invoice continues onto the next sheet with its
/// column headers repeated instead of being silently cut off at the first page break.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class A4Painter
{
    private readonly A4Document _doc;
    private int _row;      // next line-table row to draw
    private int _sumRow;   // next VAT-summary row
    private int _page;

    public A4Painter(A4Document doc) => _doc = doc;

    // Everything is in hundredths of an inch — the default page unit for a printer Graphics —
    // so a font in points comes out the physical size it says it is.
    private const float LineH = 17f;
    private const float RowH = 20f;
    private const float Pad = 5f;

    private static readonly Font Title = new("Segoe UI", 17, FontStyle.Bold);
    private static readonly Font Seller = new("Segoe UI", 13, FontStyle.Bold);
    private static readonly Font Body = new("Segoe UI", 8.5f);
    private static readonly Font BodyBold = new("Segoe UI", 8.5f, FontStyle.Bold);
    private static readonly Font Small = new("Segoe UI", 7.5f);
    private static readonly Font Head = new("Segoe UI", 8.5f, FontStyle.Bold);
    private static readonly Font Section = new("Segoe UI", 9.5f, FontStyle.Bold);
    private static readonly Font Grand = new("Segoe UI", 11, FontStyle.Bold);

    private static readonly Brush Ink = Brushes.Black;
    private static readonly Brush Dim = new SolidBrush(Color.FromArgb(110, 110, 110));
    private static readonly Pen Rule = new(Color.FromArgb(190, 190, 190), 0.6f);
    private static readonly Pen HeavyRule = new(Color.FromArgb(60, 60, 60), 1.2f);
    private static readonly Brush HeadFill = new SolidBrush(Color.FromArgb(238, 240, 244));

    public void PrintPage(object? sender, PrintPageEventArgs e)
    {
        var g = e.Graphics!;
        var area = e.MarginBounds;
        _page++;

        var y = (float)area.Top;

        if (_page == 1)
            y = DrawHeader(g, area, y);
        else
            y = DrawContinuationHeader(g, area, y);

        // The foot (summary + totals + notes + signatures) is drawn on the LAST page only, and
        // it needs room. Reserve it up front, or the rows fill the page and the totals get pushed
        // onto a sheet of their own — an invoice whose total is on page 2, alone, looks like a
        // mistake, because it is one.
        var footHeight = FootHeight(g, area);
        var rowsBottom = area.Bottom - LineH;                      // room for the page number
        var lastPageBottom = rowsBottom - footHeight;

        y = DrawTableHeader(g, area, y, _doc.Columns);

        var remaining = _doc.Rows.Count - _row;
        var fitsWithFoot = (int)Math.Floor((lastPageBottom - y) / RowH);

        // Do the remaining rows AND the foot fit on this sheet? If so this is the last page.
        // `remaining == 0` forces it even when the foot does not fit: a foot that overruns the
        // bottom margin is a blemish, but a page that defers it forever is an endless print job,
        // and the printer would run until the paper did.
        var isLastPage = remaining == 0 || remaining <= fitsWithFoot;
        var limit = isLastPage ? lastPageBottom : rowsBottom;

        while (_row < _doc.Rows.Count && y + RowH <= limit)
        {
            y = DrawRow(g, area, y, _doc.Columns, _doc.Rows[_row]);
            _row++;
        }

        g.DrawLine(HeavyRule, area.Left, y, area.Right, y);
        y += Pad;

        if (isLastPage)
        {
            y = DrawSummary(g, area, y);
            y = DrawTotals(g, area, y);
            y = DrawNotes(g, area, y);
            DrawSignatures(g, area, Math.Max(y + 12f, area.Bottom - SignatureHeight()));
        }

        DrawPageNumber(g, area, isLastPage);

        e.HasMorePages = !isLastPage;
    }

    // ── header ───────────────────────────────────────────────────────────────

    private float DrawHeader(Graphics g, Rectangle area, float y)
    {
        var top = y;

        // Seller, left.
        var sellerWidth = area.Width * 0.58f;
        for (var i = 0; i < _doc.SellerLines.Count; i++)
        {
            var font = i == 0 ? Seller : Body;
            g.DrawString(_doc.SellerLines[i], font, i == 0 ? Ink : Dim,
                new RectangleF(area.Left, y, sellerWidth, font.GetHeight(g) + 2));
            y += font.GetHeight(g) + 2;
        }

        // Title + meta, right.
        var metaX = area.Left + area.Width * 0.60f;
        var metaW = area.Right - metaX;
        var ty = top;

        var titleSize = g.MeasureString(_doc.DocumentTitle, Title);
        g.DrawString(_doc.DocumentTitle, Title, Ink, area.Right - titleSize.Width, ty);
        ty += titleSize.Height + 4;

        foreach (var f in _doc.Meta)
        {
            var font = f.Emphasis ? BodyBold : Body;
            g.DrawString(f.Key, Body, Dim, metaX, ty);
            var vs = g.MeasureString(f.Value, font);
            g.DrawString(f.Value, font, Ink, area.Right - vs.Width, ty);
            ty += LineH;
        }

        y = Math.Max(y, ty) + 8;

        if (_doc.PartyTitle is { Length: > 0 })
        {
            g.DrawLine(Rule, area.Left, y, area.Right, y);
            y += 6;
            g.DrawString(_doc.PartyTitle, Section, Ink, area.Left, y);
            y += Section.GetHeight(g) + 2;

            foreach (var f in _doc.Party)
            {
                g.DrawString(f.Key, Body, Dim, area.Left, y);
                var value = string.IsNullOrWhiteSpace(f.Value) ? "" : f.Value;
                g.DrawString(value, f.Emphasis ? BodyBold : Body, Ink, area.Left + 110, y);

                // An empty field is ruled, not blank: the shop writes the buyer in by hand on a
                // walk-in sale, and a line is what tells them they may.
                if (value.Length == 0)
                {
                    var by = y + LineH - 4;
                    g.DrawLine(Rule, area.Left + 110, by, area.Left + area.Width * 0.62f, by);
                }
                y += LineH;
            }
            y += 4;
        }

        return y + 6;
    }

    private float DrawContinuationHeader(Graphics g, Rectangle area, float y)
    {
        var meta = _doc.Meta.FirstOrDefault();
        var caption = meta is null
            ? _doc.DocumentTitle
            : $"{_doc.DocumentTitle} {meta.Value} — vazhdim";

        g.DrawString(caption, Section, Ink, area.Left, y);
        y += Section.GetHeight(g) + 4;
        g.DrawLine(Rule, area.Left, y, area.Right, y);
        return y + 8;
    }

    // ── table ────────────────────────────────────────────────────────────────

    private float DrawTableHeader(Graphics g, Rectangle area, float y, List<A4Column> cols)
    {
        var widths = Widths(cols, area.Width);
        g.FillRectangle(HeadFill, area.Left, y, area.Width, RowH);

        var x = (float)area.Left;
        for (var i = 0; i < cols.Count; i++)
        {
            DrawCell(g, cols[i].Header, Head, Ink, x, y, widths[i], cols[i].Align);
            x += widths[i];
        }

        y += RowH;
        g.DrawLine(HeavyRule, area.Left, y, area.Right, y);
        return y + 2;
    }

    private float DrawRow(Graphics g, Rectangle area, float y, List<A4Column> cols, A4Row row)
    {
        var widths = Widths(cols, area.Width);
        var font = row.Emphasis ? BodyBold : Body;
        var x = (float)area.Left;

        for (var i = 0; i < cols.Count && i < row.Cells.Count; i++)
        {
            DrawCell(g, row.Cells[i], font, Ink, x, y, widths[i], cols[i].Align);
            x += widths[i];
        }

        y += RowH;
        g.DrawLine(Rule, area.Left, y, area.Right, y);
        return y;
    }

    /// <summary>
    /// One cell, clipped to its column. A price that overflows its column and paints over the
    /// next one is worse than a truncated product name: the reader cannot tell which number
    /// belongs to which heading.
    /// </summary>
    private static void DrawCell(Graphics g, string text, Font font, Brush brush,
                                 float x, float y, float w, int align)
    {
        var fmt = align switch { 1 => CellCentre, 2 => CellRight, _ => CellLeft };
        g.DrawString(text, font, brush, new RectangleF(x + Pad, y, w - 2 * Pad, RowH), fmt);
    }

    private static StringFormat Cell(StringAlignment alignment) => new(StringFormatFlags.NoWrap)
    {
        Alignment = alignment,
        LineAlignment = StringAlignment.Center,
        Trimming = StringTrimming.EllipsisCharacter,
    };

    private static readonly StringFormat CellLeft = Cell(StringAlignment.Near);
    private static readonly StringFormat CellCentre = Cell(StringAlignment.Center);
    private static readonly StringFormat CellRight = Cell(StringAlignment.Far);

    private static float[] Widths(List<A4Column> cols, int totalWidth)
    {
        var sum = cols.Sum(c => c.Weight);
        if (sum <= 0) sum = cols.Count;
        return cols.Select(c => (float)(totalWidth * c.Weight / sum)).ToArray();
    }

    // ── foot ─────────────────────────────────────────────────────────────────

    private float DrawSummary(Graphics g, Rectangle area, float y)
    {
        if (_doc.SummaryTitle is not { Length: > 0 } || _doc.SummaryRows.Count == 0) return y;

        y += 10;
        g.DrawString(_doc.SummaryTitle, Section, Ink, area.Left, y);
        y += Section.GetHeight(g) + 3;

        // Half-width, on the left — the totals sit opposite it, on the right.
        var half = new Rectangle(area.Left, area.Top, (int)(area.Width * 0.46f), area.Height);
        y = DrawTableHeader(g, half, y, _doc.SummaryColumns);

        while (_sumRow < _doc.SummaryRows.Count)
        {
            y = DrawRow(g, half, y, _doc.SummaryColumns, _doc.SummaryRows[_sumRow]);
            _sumRow++;
        }

        return y;
    }

    private float DrawTotals(Graphics g, Rectangle area, float y)
    {
        if (_doc.Totals.Count == 0) return y;

        // Totals hang on the right, and the VAT summary was drawn on the left of the same band —
        // so back up to where that band started rather than stacking below it.
        var ty = y - (_doc.SummaryRows.Count + 1) * RowH - 20;
        if (_doc.SummaryRows.Count == 0 || ty < area.Top) ty = y + 10;

        var x = area.Left + area.Width * 0.55f;

        foreach (var f in _doc.Totals)
        {
            var isGrand = f.Emphasis;
            var font = isGrand ? Grand : Body;

            if (isGrand)
            {
                g.DrawLine(HeavyRule, x, ty, area.Right, ty);
                ty += 4;
            }

            var h = font.GetHeight(g) + 4;
            g.DrawString(f.Key, isGrand ? Grand : Body, isGrand ? Ink : Dim, x, ty);
            var vs = g.MeasureString(f.Value, font);
            g.DrawString(f.Value, font, Ink, area.Right - vs.Width, ty);
            ty += h;
        }

        return Math.Max(y, ty) + 6;
    }

    private float DrawNotes(Graphics g, Rectangle area, float y)
    {
        foreach (var note in _doc.Notes)
        {
            var h = g.MeasureString(note, Small, area.Width).Height + 2;
            g.DrawString(note, Small, Dim, new RectangleF(area.Left, y, area.Width, h));
            y += h;
        }
        return y;
    }

    private void DrawSignatures(Graphics g, Rectangle area, float y)
    {
        if (_doc.Signatures.Count == 0) return;

        var slot = area.Width / (float)_doc.Signatures.Count;
        var lineY = y + 22;

        for (var i = 0; i < _doc.Signatures.Count; i++)
        {
            var x = area.Left + i * slot;
            var w = slot * 0.8f;
            g.DrawLine(Rule, x, lineY, x + w, lineY);
            g.DrawString(_doc.Signatures[i], Small, Dim, x, lineY + 3);
        }
    }

    private float SignatureHeight() => _doc.Signatures.Count == 0 ? 0 : 44f;

    private float FootHeight(Graphics g, Rectangle area)
    {
        var h = 12f;
        if (_doc.SummaryRows.Count > 0)
            h += Section.GetHeight(g) + 3 + RowH + _doc.SummaryRows.Count * RowH + 10;

        var totals = _doc.Totals.Sum(f => (f.Emphasis ? Grand : Body).GetHeight(g) + 4) + 10;
        h = Math.Max(h, totals + 12);

        h += _doc.Notes.Sum(n => g.MeasureString(n, Small, area.Width).Height + 2);
        h += SignatureHeight() + 12;
        return h;
    }

    private void DrawPageNumber(Graphics g, Rectangle area, bool isLast)
    {
        var text = isLast && _page == 1 ? "" : $"Faqe {_page}";
        if (text.Length == 0) return;

        var size = g.MeasureString(text, Small);
        g.DrawString(text, Small, Dim, area.Right - size.Width, area.Bottom - size.Height + 6);
    }
}
