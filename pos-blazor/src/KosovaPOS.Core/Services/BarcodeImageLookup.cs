using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace KosovaPOS.Core.Services;

/// <summary>A product some public barcode database thinks this EAN is, with pictures of it.</summary>
public sealed record BarcodeCandidate(string Title, string? Brand, IReadOnlyList<string> Images, string Source);

/// <summary>
/// Given a barcode, proposes pictures of the product it identifies.
///
/// This exists because of a hard limit in the shop's own data: its article names are a
/// generic Albanian noun plus an internal code — "Loder 0115012" is *Toy 0115012* — so
/// there is nothing to search the internet WITH. Name-based image search would return a
/// toy, confidently, and it would be the wrong toy on a live storefront that takes real
/// money. A barcode is different: an EAN-13 names one manufactured product, globally, and
/// looking it up returns that product's own photographs.
///
/// So this only ever runs against a checksum-valid EAN — <see cref="IsValidEan"/> is the
/// gate — and it PROPOSES. A human presses the button that attaches the picture. Roughly
/// 255 of this shop's 1,619 articles have a real barcode; for the rest, somebody has to
/// take a photograph, and no API can change that.
/// </summary>
public class BarcodeImageLookup
{
    private readonly IHttpClientFactory _http;
    private readonly ILogger<BarcodeImageLookup> _log;

    public BarcodeImageLookup(IHttpClientFactory http, ILogger<BarcodeImageLookup> log)
    {
        _http = http;
        _log = log;
    }

    /// <summary>
    /// A GS1 check-digit test. Not cosmetic: most of this shop's "barcodes" are internal
    /// codes like "00080" or "29", and sending those to a product database returns either
    /// nothing or — worse — some unrelated product that happens to share the digits.
    /// </summary>
    public static bool IsValidEan(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return false;

        var code = barcode.Trim();
        if (code.Length is not (8 or 13) || !code.All(char.IsAsciiDigit)) return false;

        var digits = code.Select(c => c - '0').ToArray();

        // EAN-13 weights the odd positions by 3 counting from the right; EAN-8 weights the
        // even ones. Indexing from the left, that flips with the length.
        var sum = 0;
        for (var i = 0; i < digits.Length - 1; i++)
        {
            var weight = code.Length == 13
                ? (i % 2 == 0 ? 1 : 3)
                : (i % 2 == 0 ? 3 : 1);
            sum += digits[i] * weight;
        }

        var check = (10 - sum % 10) % 10;
        return check == digits[^1];
    }

    /// <summary>
    /// What the public barcode databases have for this EAN. Empty is the normal answer for
    /// cheap unbranded imports, which is most of this shop — the caller must treat "no
    /// candidates" as ordinary, not as an error.
    /// </summary>
    public async Task<IReadOnlyList<BarcodeCandidate>> LookupAsync(string barcode, CancellationToken ct = default)
    {
        if (!IsValidEan(barcode))
            return [];

        var code = barcode.Trim();
        var found = new List<BarcodeCandidate>();

        foreach (var probe in (Func<string, CancellationToken, Task<BarcodeCandidate?>>[])[OpenFoodFactsAsync, UpcItemDbAsync])
        {
            try
            {
                if (await probe(code, ct) is { } candidate && candidate.Images.Count > 0)
                    found.Add(candidate);
            }
            catch (Exception e)
            {
                // One database being down or rate-limiting must not take the screen with it.
                _log.LogWarning(e, "Barcode lookup failed for {Barcode}.", code);
            }
        }

        return found;
    }

    /// <summary>Open Food Facts: free, no key, and covers the cosmetics this shop sells.</summary>
    private async Task<BarcodeCandidate?> OpenFoodFactsAsync(string code, CancellationToken ct)
    {
        var client = _http.CreateClient(nameof(BarcodeImageLookup));
        var url = $"https://world.openfoodfacts.org/api/v2/product/{code}.json?fields=product_name,brands,image_url,image_front_url";

        using var response = await client.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return null;

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!json.RootElement.TryGetProperty("product", out var product)) return null;

        var images = new[] { "image_front_url", "image_url" }
            .Select(f => product.TryGetProperty(f, out var v) ? v.GetString() : null)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u!)
            .Distinct()
            .ToList();

        if (images.Count == 0) return null;

        return new BarcodeCandidate(
            product.TryGetProperty("product_name", out var n) ? n.GetString() ?? code : code,
            product.TryGetProperty("brands", out var b) ? b.GetString() : null,
            images,
            "Open Food Facts");
    }

    /// <summary>
    /// UPCitemdb's trial endpoint: no key, rate-limited to ~100 lookups a day per IP. That
    /// budget is why this is a per-article button and not a sweep over the whole catalogue.
    /// </summary>
    private async Task<BarcodeCandidate?> UpcItemDbAsync(string code, CancellationToken ct)
    {
        var client = _http.CreateClient(nameof(BarcodeImageLookup));
        var url = $"https://api.upcitemdb.com/prod/trial/lookup?upc={code}";

        using var response = await client.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return null;

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!json.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
            return null;

        var item = items[0];
        var images = item.TryGetProperty("images", out var imgs)
            ? imgs.EnumerateArray().Select(i => i.GetString()).Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u!).Take(5).ToList()
            : [];

        if (images.Count == 0) return null;

        return new BarcodeCandidate(
            item.TryGetProperty("title", out var t) ? t.GetString() ?? code : code,
            item.TryGetProperty("brand", out var b) ? b.GetString() : null,
            images,
            "UPCitemdb");
    }
}
