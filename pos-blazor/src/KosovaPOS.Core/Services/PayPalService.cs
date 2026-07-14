using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using KosovaPOS.Models.Shop;

namespace KosovaPOS.Core.Services;

public sealed class PayPalOptions
{
    public string ClientId { get; init; } = "";
    public string Secret { get; init; } = "";

    /// <summary>"production" or "sandbox".</summary>
    public string Environment { get; init; } = "sandbox";

    public bool Configured => ClientId.Length > 0 && Secret.Length > 0;
    public bool IsLive => string.Equals(Environment, "production", StringComparison.OrdinalIgnoreCase);

    public string BaseUrl => IsLive
        ? "https://api-m.paypal.com"
        : "https://api-m.sandbox.paypal.com";

    public static PayPalOptions FromEnvironment() => new()
    {
        ClientId = System.Environment.GetEnvironmentVariable("PAYPAL_CLIENT_ID") ?? "",
        Secret = System.Environment.GetEnvironmentVariable("PAYPAL_CLIENT_SECRET") ?? "",
        Environment = System.Environment.GetEnvironmentVariable("PAYPAL_ENVIRONMENT") ?? "sandbox",
    };
}

public sealed record PayPalOrder(string Id, string ApproveUrl);
public sealed record PayPalCapture(bool Completed, string? CaptureId, string? Error);

/// <summary>
/// PayPal Orders v2, server-side.
///
/// The whole flow lives on the server and the browser is never trusted with an amount:
/// we create the order with a total we computed from the database, PayPal shows the
/// customer that amount, and we then ask PayPal what it actually captured. A client-side
/// integration where JavaScript names the price is a shop that can be bought out for one
/// cent by anyone who can open dev tools.
///
/// <c>PAYPAL_ENVIRONMENT=production</c> means this moves real money. There is no test mode
/// hiding behind a flag here — if it is live, it is live.
/// </summary>
public class PayPalService
{
    private readonly PayPalOptions _options;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<PayPalService> _log;

    // PayPal's access tokens last ~9 hours. Cached because minting one is a network
    // round-trip that would otherwise sit in front of every single checkout.
    private string? _token;
    private DateTimeOffset _tokenExpires = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public PayPalService(PayPalOptions options, IHttpClientFactory http, ILogger<PayPalService> log)
    {
        _options = options;
        _http = http;
        _log = log;
    }

    public bool Configured => _options.Configured;
    public bool IsLive => _options.IsLive;

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _tokenExpires)
            return _token;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _tokenExpires)
                return _token;

            var client = _http.CreateClient(nameof(PayPalService));

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/v1/oauth2/token");
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.Secret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            request.Content = new FormUrlEncodedContent([new("grant_type", "client_credentials")]);

            using var response = await client.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var token = json.RootElement.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("PayPal returned no access_token.");
            var seconds = json.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3000;

            _token = token;
            // A minute of slack, so a token cannot expire between the check and the call.
            _tokenExpires = DateTimeOffset.UtcNow.AddSeconds(seconds - 60);
            return token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>
    /// Creates the PayPal order and returns the URL to send the customer to.
    ///
    /// <paramref name="order"/>'s total was computed by <see cref="ShopService"/> from the
    /// database. It is the only amount PayPal is ever told.
    /// </summary>
    public async Task<PayPalOrder?> CreateOrderAsync(
        WebOrder order, string returnUrl, string cancelUrl, string brandName, CancellationToken ct = default)
    {
        if (!_options.Configured)
        {
            _log.LogError("PayPal is not configured; cannot create an order.");
            return null;
        }

        var payload = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    reference_id = order.OrderNumber,
                    custom_id = order.Id.ToString(),
                    description = $"Porosia {order.OrderNumber}",
                    amount = new
                    {
                        currency_code = "EUR",
                        value = order.Total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    }
                }
            },
            payment_source = new
            {
                paypal = new
                {
                    experience_context = new
                    {
                        brand_name = brandName,
                        landing_page = "LOGIN",
                        user_action = "PAY_NOW",
                        shipping_preference = "NO_SHIPPING",   // we collected the address ourselves
                        return_url = returnUrl,
                        cancel_url = cancelUrl,
                    }
                }
            }
        };

        try
        {
            var client = _http.CreateClient(nameof(PayPalService));

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/v2/checkout/orders");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(ct));
            request.Content = JsonContent.Create(payload);

            using var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _log.LogError("PayPal create-order failed ({Status}): {Body}", (int)response.StatusCode, body);
                return null;
            }

            using var json = JsonDocument.Parse(body);
            var id = json.RootElement.GetProperty("id").GetString();
            if (id is null) return null;

            var approve = json.RootElement.TryGetProperty("links", out var links)
                ? links.EnumerateArray()
                    .FirstOrDefault(l => l.TryGetProperty("rel", out var rel) && rel.GetString() == "payer-action")
                : default;

            // PayPal calls it "payer-action" when a payment_source is supplied and "approve"
            // otherwise. Accepting both means the flow does not break if that changes back.
            var url = approve.ValueKind == JsonValueKind.Object
                ? approve.GetProperty("href").GetString()
                : json.RootElement.GetProperty("links").EnumerateArray()
                    .FirstOrDefault(l => l.TryGetProperty("rel", out var rel) && rel.GetString() == "approve")
                    .GetProperty("href").GetString();

            return url is null ? null : new PayPalOrder(id, url);
        }
        catch (Exception e)
        {
            _log.LogError(e, "PayPal create-order threw.");
            return null;
        }
    }

    /// <summary>
    /// Takes the money. Returns completed only when PayPal says the capture status is
    /// COMPLETED — the customer arriving back on the return URL proves they clicked a
    /// button, not that anyone was charged.
    ///
    /// PayPal treats a repeated capture of an already-captured order as an error
    /// (ORDER_ALREADY_CAPTURED). That is reported as a failure here, and the caller
    /// handles it by checking whether the order is already marked paid — which it is,
    /// because that is the only way it could have been captured.
    /// </summary>
    public async Task<PayPalCapture> CaptureOrderAsync(string payPalOrderId, CancellationToken ct = default)
    {
        if (!_options.Configured)
            return new PayPalCapture(false, null, "PayPal is not configured.");

        try
        {
            var client = _http.CreateClient(nameof(PayPalService));

            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"{_options.BaseUrl}/v2/checkout/orders/{payPalOrderId}/capture");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(ct));
            // PayPal requires a body on capture, even an empty one.
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _log.LogError("PayPal capture failed ({Status}): {Body}", (int)response.StatusCode, body);
                return new PayPalCapture(false, null, $"HTTP {(int)response.StatusCode}");
            }

            using var json = JsonDocument.Parse(body);

            var status = json.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;
            if (!string.Equals(status, "COMPLETED", StringComparison.Ordinal))
            {
                _log.LogWarning("PayPal capture returned status {Status} for {Order}.", status, payPalOrderId);
                return new PayPalCapture(false, null, status ?? "unknown");
            }

            var captureId = json.RootElement
                .GetProperty("purchase_units")[0]
                .GetProperty("payments")
                .GetProperty("captures")[0]
                .GetProperty("id").GetString();

            return new PayPalCapture(true, captureId, null);
        }
        catch (Exception e)
        {
            _log.LogError(e, "PayPal capture threw for {Order}.", payPalOrderId);
            return new PayPalCapture(false, null, e.Message);
        }
    }
}
