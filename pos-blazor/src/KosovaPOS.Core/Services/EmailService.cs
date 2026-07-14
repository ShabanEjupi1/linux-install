using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using KosovaPOS.Models;
using KosovaPOS.Models.Shop;

namespace KosovaPOS.Core.Services;

/// <summary>
/// SMTP settings, from the environment. Never from the database: they are a credential.
/// </summary>
public sealed class EmailOptions
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 587;
    public string User { get; init; } = "";
    public string Password { get; init; } = "";
    public string FromAddress { get; init; } = "";
    public string FromName { get; init; } = "";

    /// <summary>False when no SMTP host is configured — the app runs fine, it just cannot send.</summary>
    public bool Configured => Host.Length > 0 && FromAddress.Length > 0;

    public static EmailOptions FromEnvironment() => new()
    {
        Host = Environment.GetEnvironmentVariable("POS_SMTP_HOST") ?? "",
        Port = int.TryParse(Environment.GetEnvironmentVariable("POS_SMTP_PORT"), out var p) ? p : 587,
        User = Environment.GetEnvironmentVariable("POS_SMTP_USER") ?? "",
        Password = Environment.GetEnvironmentVariable("POS_SMTP_PASSWORD") ?? "",
        FromAddress = Environment.GetEnvironmentVariable("POS_SMTP_FROM") ?? "",
        FromName = Environment.GetEnvironmentVariable("POS_SMTP_FROM_NAME") ?? "",
    };
}

/// <summary>
/// Sends the two emails an order produces: a confirmation to the customer, and a heads-up
/// to the shop. Goes out through the business's own Mailcow on port 587 (STARTTLS), which
/// relays onward via Brevo — so mail from the shop is signed by the shop's own DKIM key
/// and does not land in spam.
///
/// Every send is best-effort and swallowed. An order that is paid for is paid for; if the
/// mail server is down, the customer must still get their receipt page and the shop must
/// still get the order in <c>/porosite</c>. Losing the confirmation email is annoying.
/// Throwing here, after PayPal has already taken the money, would lose the ORDER — which
/// is why this class never lets an exception past.
/// </summary>
public class EmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailService> _log;

    public EmailService(EmailOptions options, ILogger<EmailService> log)
    {
        _options = options;
        _log = log;
    }

    public bool Configured => _options.Configured;

    public async Task<bool> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!_options.Configured)
        {
            _log.LogWarning("Not sending mail to {To} ({Subject}): no SMTP host configured.", to, subject);
            return false;
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_options.FromAddress,
                    _options.FromName.Length > 0 ? _options.FromName : _options.FromAddress),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
            };
            message.To.Add(to);

            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = true,   // STARTTLS on 587
                Credentials = new NetworkCredential(_options.User, _options.Password),
                Timeout = 20_000,
            };

            await client.SendMailAsync(message, ct);
            _log.LogInformation("Sent \"{Subject}\" to {To}.", subject, to);
            return true;
        }
        catch (Exception e)
        {
            _log.LogError(e, "Failed to send \"{Subject}\" to {To}.", subject, to);
            return false;
        }
    }

    // ── The order emails ────────────────────────────────────────────────

    public Task SendOrderConfirmationAsync(WebOrder order, BusinessSettings shop, string shopUrl, CancellationToken ct = default)
    {
        var shopName = shop.BusinessName ?? "Dyqani";

        var body = $"""
            <div style="font-family:system-ui,Segoe UI,Arial,sans-serif;max-width:560px;margin:0 auto;color:#111">
              <h2 style="margin:0 0 4px">Faleminderit për porosinë!</h2>
              <p style="margin:0 0 20px;color:#555">Porosia juaj te <strong>{Escape(shopName)}</strong> u pranua.</p>

              <p style="margin:0 0 6px">Numri i porosisë: <strong>{Escape(order.OrderNumber)}</strong></p>
              <p style="margin:0 0 20px">Pagesa: <strong>{PaymentLabel(order)}</strong></p>

              {ItemsTable(order)}

              <h3 style="margin:24px 0 6px">Adresa e dërgesës</h3>
              <p style="margin:0;color:#555;line-height:1.5">
                {Escape(order.CustomerName)}<br>
                {Escape(order.Address)}{(string.IsNullOrWhiteSpace(order.City) ? "" : "<br>" + Escape(order.City!))}
                {(string.IsNullOrWhiteSpace(order.Phone) ? "" : "<br>" + Escape(order.Phone!))}
              </p>

              <p style="margin:28px 0 0;color:#777;font-size:13px">
                Do t'ju kontaktojmë për dërgesën. Për çdo pyetje, përgjigjuni këtij emaili.<br>
                <a href="{Escape(shopUrl)}" style="color:#c2185b">{Escape(shopUrl)}</a>
              </p>
            </div>
            """;

        return SendAsync(order.Email, $"Porosia {order.OrderNumber} — {shopName}", body, ct);
    }

    public Task SendShopNotificationAsync(WebOrder order, BusinessSettings shop, string shopUrl, CancellationToken ct = default)
    {
        var to = !string.IsNullOrWhiteSpace(shop.ShopOrderEmail) ? shop.ShopOrderEmail!
               : !string.IsNullOrWhiteSpace(shop.Email) ? shop.Email!
               : _options.FromAddress;

        if (string.IsNullOrWhiteSpace(to))
            return Task.CompletedTask;

        var body = $"""
            <div style="font-family:system-ui,Segoe UI,Arial,sans-serif;max-width:560px;margin:0 auto;color:#111">
              <h2 style="margin:0 0 4px">Porosi e re — {Escape(order.OrderNumber)}</h2>
              <p style="margin:0 0 20px;color:#555">Pagesa: <strong>{PaymentLabel(order)}</strong></p>

              {ItemsTable(order)}

              <h3 style="margin:24px 0 6px">Klienti</h3>
              <p style="margin:0;color:#555;line-height:1.5">
                {Escape(order.CustomerName)}<br>
                {Escape(order.Email)}{(string.IsNullOrWhiteSpace(order.Phone) ? "" : "<br>" + Escape(order.Phone!))}<br>
                {Escape(order.Address)}{(string.IsNullOrWhiteSpace(order.City) ? "" : ", " + Escape(order.City!))}
              </p>
              {(string.IsNullOrWhiteSpace(order.Note) ? "" :
                $"<h3 style=\"margin:24px 0 6px\">Shënim</h3><p style=\"margin:0;color:#555\">{Escape(order.Note!)}</p>")}
            </div>
            """;

        return SendAsync(to, $"Porosi e re {order.OrderNumber} ({order.Total:0.00} €)", body, ct);
    }

    private static string PaymentLabel(WebOrder o) => o.PaymentMethod switch
    {
        WebPaymentMethod.PayPal => o.PaymentStatus == WebPaymentStatus.Paid
            ? "PayPal — e paguar"
            : "PayPal — në pritje",
        _ => "Para në dorë (në dorëzim)",
    };

    private static string ItemsTable(WebOrder order)
    {
        var rows = string.Join("", order.Items.Select(i => $"""
            <tr>
              <td style="padding:8px 0;border-bottom:1px solid #eee">{Escape(i.Name)}</td>
              <td style="padding:8px 0;border-bottom:1px solid #eee;text-align:center;color:#666">{i.Quantity:0.##}</td>
              <td style="padding:8px 0;border-bottom:1px solid #eee;text-align:right">{i.LineTotal:0.00} €</td>
            </tr>
            """));

        return $"""
            <table style="width:100%;border-collapse:collapse;font-size:14px">
              {rows}
              <tr>
                <td style="padding:8px 0" colspan="2">Nëntotali</td>
                <td style="padding:8px 0;text-align:right">{order.Subtotal:0.00} €</td>
              </tr>
              <tr>
                <td style="padding:0 0 8px" colspan="2">Transporti</td>
                <td style="padding:0 0 8px;text-align:right">{(order.ShippingFee == 0 ? "Falas" : $"{order.ShippingFee:0.00} €")}</td>
              </tr>
              <tr>
                <td style="padding:10px 0;border-top:2px solid #111;font-weight:700" colspan="2">Totali</td>
                <td style="padding:10px 0;border-top:2px solid #111;font-weight:700;text-align:right">{order.Total:0.00} €</td>
              </tr>
            </table>
            """;
    }

    /// <summary>
    /// The customer's own name and address are interpolated into this HTML, and they typed
    /// them. Unescaped, a name like <c>&lt;script&gt;</c> would be markup in the shop's inbox.
    /// </summary>
    private static string Escape(string value) => WebUtility.HtmlEncode(value);
}
