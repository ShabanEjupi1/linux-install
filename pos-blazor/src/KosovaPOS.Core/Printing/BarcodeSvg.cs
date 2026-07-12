using System.Text;

namespace KosovaPOS.Core.Printing;

/// <summary>Which symbology a code was encoded as, so the label can say so (and a test can assert it).</summary>
public enum BarcodeKind { None, Ean13, Ean8, Code128 }

/// <summary>
/// Renders a barcode as SVG so the BROWSER can print a scannable label — no agent, no
/// printer driver commands, nothing installed on the shop PC.
///
/// Why this exists: the desktop drove the HPRT with raw TSPL (<c>BARCODE x,y,"128",…</c>),
/// and the printer's own firmware drew the bars. A web page cannot send raw bytes to a
/// printer, so the bars have to be drawn here and sent through the Windows driver as
/// graphics — which is exactly what the desktop's own <c>GenerateTSPLWithBitmap</c>
/// fallback did when TSPL misbehaved. Same picture, different transport.
///
/// Symbology follows the desktop's rule (<c>GetBarcodeType</c>): 13 digits → EAN-13,
/// 8 digits → EAN-8, anything else printable → Code 128. A code that cannot be encoded
/// (empty, or non-ASCII) returns <see cref="BarcodeKind.None"/> and the caller prints it
/// as plain text — again what the desktop did, rather than printing a wrong barcode.
///
/// The bars are drawn in <b>millimetres</b>. A barcode scaled by the browser's pixel
/// mapping would be at the mercy of the print scale factor; scanners care about the
/// physical width of the narrow bar, so the narrow bar is specified in mm and everything
/// else is a multiple of it.
/// </summary>
public static class BarcodeSvg
{
    /// <summary>
    /// The narrow-bar width. 0.33mm ≈ the 2-dot narrow bar the desktop asked TSPL for on a
    /// 203dpi label printer, and comfortably above the ~0.25mm floor most scanners manage.
    /// </summary>
    public const double DefaultModuleMm = 0.33;

    public static BarcodeKind KindOf(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return BarcodeKind.None;
        var s = content.Trim();

        if (s.Length == 13 && s.All(char.IsAsciiDigit)) return BarcodeKind.Ean13;
        if (s.Length == 8 && s.All(char.IsAsciiDigit)) return BarcodeKind.Ean8;

        // Code 128 subset B covers ASCII 32..126. Anything outside it (an accented name
        // typed into the barcode field) is not encodable and must not be faked.
        return s.All(c => c >= 32 && c <= 126) ? BarcodeKind.Code128 : BarcodeKind.None;
    }

    /// <summary>
    /// The barcode as a standalone SVG element, or null if <paramref name="content"/> cannot
    /// be encoded. <paramref name="heightMm"/> is the bar height, excluding the human-readable
    /// digits (the caller renders those as text, so they stay crisp).
    /// </summary>
    public static string? Render(string? content, double heightMm = 10, double moduleMm = DefaultModuleMm)
    {
        var kind = KindOf(content);
        if (kind == BarcodeKind.None) return null;

        var s = content!.Trim();
        var modules = kind switch
        {
            BarcodeKind.Ean13   => EncodeEan(s, 13),
            BarcodeKind.Ean8    => EncodeEan(s, 8),
            _                   => EncodeCode128B(s),
        };
        if (modules is null) return null;

        var widthMm = modules.Length * moduleMm;
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {F(widthMm)} {F(heightMm)}\" ")
          .Append($"width=\"{F(widthMm)}mm\" height=\"{F(heightMm)}mm\" shape-rendering=\"crispEdges\">");

        // One <rect> per run of black modules rather than per module: a 95-rect EAN-13 is
        // fine, but a long Code 128 becomes hundreds of elements the print pipeline has to
        // rasterise, and adjacent rects can seam at low DPI.
        int i = 0;
        while (i < modules.Length)
        {
            if (!modules[i]) { i++; continue; }

            int run = 0;
            while (i + run < modules.Length && modules[i + run]) run++;

            sb.Append($"<rect x=\"{F(i * moduleMm)}\" y=\"0\" width=\"{F(run * moduleMm)}\" height=\"{F(heightMm)}\" fill=\"#000\"/>");
            i += run;
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>The printed width, so a caller can centre the symbol without measuring the DOM.</summary>
    public static double WidthMm(string? content, double moduleMm = DefaultModuleMm)
    {
        var kind = KindOf(content);
        if (kind == BarcodeKind.None) return 0;

        var modules = kind switch
        {
            BarcodeKind.Ean13 => EncodeEan(content!.Trim(), 13),
            BarcodeKind.Ean8  => EncodeEan(content!.Trim(), 8),
            _                 => EncodeCode128B(content!.Trim()),
        };
        return (modules?.Length ?? 0) * moduleMm;
    }

    /// <summary>
    /// EAN-13/EAN-8 check digit (mod 10, weights 3/1 from the right). Public because the label
    /// page needs to tell a shop that a 13-digit code in the catalogue is not a valid EAN —
    /// the scanner will simply refuse it, and silently printing it would waste a roll of labels.
    /// </summary>
    public static int EanCheckDigit(string digitsWithoutCheck)
    {
        int sum = 0, weight = 3;
        for (int i = digitsWithoutCheck.Length - 1; i >= 0; i--)
        {
            sum += (digitsWithoutCheck[i] - '0') * weight;
            weight = weight == 3 ? 1 : 3;
        }
        return (10 - sum % 10) % 10;
    }

    public static bool EanCheckDigitValid(string full) =>
        full.Length > 1 && full.All(char.IsAsciiDigit) &&
        EanCheckDigit(full[..^1]) == full[^1] - '0';

    // ── EAN-13 / EAN-8 ──────────────────────────────────────────────────
    private static readonly string[] L =
        ["0001101","0011001","0010011","0111101","0100011","0110001","0101111","0111011","0110111","0001011"];
    private static readonly string[] G =
        ["0100111","0110011","0011011","0100001","0011101","0111001","0000101","0010001","0001001","0010111"];
    private static readonly string[] R =
        ["1110010","1100110","1101100","1000010","1011100","1001110","1010000","1000100","1001000","1110100"];

    /// <summary>Which of the first six digits use G-parity, selected by the leading digit. This is what carries the 13th digit.</summary>
    private static readonly string[] Parity =
        ["LLLLLL","LLGLGG","LLGGLG","LLGGGL","LGLLGG","LGGLLG","LGGGLL","LGLGLG","LGLGGL","LGGLGL"];

    private static bool[]? EncodeEan(string s, int length)
    {
        if (s.Length != length || !s.All(char.IsAsciiDigit))
            return null;

        // A wrong check digit is a code no scanner will read. Refuse rather than print it.
        if (!EanCheckDigitValid(s))
            return null;

        var bits = new StringBuilder();

        if (length == 13)
        {
            var parity = Parity[s[0] - '0'];
            bits.Append("101");
            for (int i = 1; i <= 6; i++)
                bits.Append(parity[i - 1] == 'L' ? L[s[i] - '0'] : G[s[i] - '0']);
            bits.Append("01010");
            for (int i = 7; i <= 12; i++)
                bits.Append(R[s[i] - '0']);
            bits.Append("101");
        }
        else
        {
            bits.Append("101");
            for (int i = 0; i < 4; i++) bits.Append(L[s[i] - '0']);
            bits.Append("01010");
            for (int i = 4; i < 8; i++) bits.Append(R[s[i] - '0']);
            bits.Append("101");
        }

        return bits.ToString().Select(c => c == '1').ToArray();
    }

    // ── Code 128 (subset B) ─────────────────────────────────────────────
    // Each entry is the bar/space run-length pattern for one symbol value, starting with a bar.
    private static readonly string[] C128 =
    [
        "212222","222122","222221","121223","121322","131222","122213","122312","132212","221213",
        "221312","231212","112232","122132","122231","113222","123122","123221","223211","221132",
        "221231","213212","223112","312131","311222","321122","321221","312212","322112","322211",
        "212123","212321","232121","111323","131123","131321","112313","132113","132311","211313",
        "231113","231311","112133","112331","132131","113123","113321","133121","313121","211331",
        "231131","213113","213311","213131","311123","311321","331121","312113","312311","332111",
        "314111","221411","431111","111224","111422","121124","121421","141122","141221","112214",
        "112412","122114","122411","142112","142211","241211","221114","413111","241112","134111",
        "111242","121142","121241","114212","124112","124211","411212","421112","421211","212141",
        "214121","412121","111143","111341","131141","114113","114311","411113","411311","113141",
        "114131","311141","411131","211412","211214","211232","2331112",
    ];

    private const int StartB = 104;
    private const int Stop = 106;

    private static bool[]? EncodeCode128B(string s)
    {
        if (s.Length == 0 || s.Any(c => c < 32 || c > 126))
            return null;

        var values = new List<int> { StartB };
        foreach (var c in s)
            values.Add(c - 32);           // subset B: value = ASCII - 32

        // Checksum: start value + sum(value_i * position_i), mod 103.
        long sum = StartB;
        for (int i = 1; i < values.Count; i++)
            sum += (long)values[i] * i;
        values.Add((int)(sum % 103));
        values.Add(Stop);

        var bits = new List<bool>();
        foreach (var v in values)
        {
            var pattern = C128[v];
            bool black = true;                     // every pattern starts with a bar
            foreach (var runChar in pattern)
            {
                int run = runChar - '0';
                for (int i = 0; i < run; i++) bits.Add(black);
                black = !black;
            }
        }

        return bits.ToArray();
    }

    private static string F(double mm) => mm.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
