using KosovaPOS.Core.Data;

namespace KosovaPOS.Core.Services;

/// <summary>
/// Maps between a business code and the first-level hostname that reaches it —
/// <c>pos-&lt;code&gt;.spacecode.tech</c>.
///
/// First-level (one label) on purpose: Cloudflare's free Universal SSL covers the
/// apex and one level of subdomain, so <c>pos-bmd.spacecode.tech</c> is inside the
/// <c>*.spacecode.tech</c> wildcard cert at no cost, while a two-level name like
/// <c>bmd.pos.spacecode.tech</c> would need paid Advanced Certificate Manager.
///
/// The <c>pos-</c> prefix keeps tenant hostnames in their own namespace so a
/// business code can never shadow an infrastructure subdomain (mail, git, ssh …),
/// and — because the whole thing is still one label — a single <c>*.spacecode.tech</c>
/// DNS record and one cloudflared ingress rule route every present and future shop
/// with no per-shop configuration. cloudflared only treats a rule as a wildcard
/// when it starts with "*.", so <c>pos-*</c> is not expressible at the edge anyway;
/// this class is where the <c>pos-</c> scoping actually lives.
/// </summary>
public sealed class BusinessHostResolver
{
    /// <summary>e.g. "spacecode.tech" — the zone apex, lowercase, no leading dot.</summary>
    public string BaseHost { get; }

    /// <summary>e.g. "pos-" — the label prefix that marks a host as a business host.</summary>
    public string Prefix { get; }

    private readonly string _suffix; // ".spacecode.tech"

    public BusinessHostResolver(string baseHost, string prefix)
    {
        BaseHost = (baseHost ?? "").Trim().Trim('.').ToLowerInvariant();
        if (BaseHost.Length == 0)
            throw new ArgumentException("Base host is required.", nameof(baseHost));
        Prefix = (prefix ?? "").Trim().ToLowerInvariant();
        _suffix = "." + BaseHost;
    }

    /// <summary>
    /// The business code a request Host maps to, or null when the host is not a
    /// business subdomain (the zone apex, an infra subdomain, localhost in dev, or
    /// anything malformed). A null result means "fall back to the code field on the
    /// login form" — it is not an error.
    /// </summary>
    public string? CodeFromHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return null;

        host = host.Trim().ToLowerInvariant();

        // Strip a port if the Host header carries one (dev: localhost:5199).
        var colon = host.IndexOf(':');
        if (colon >= 0)
            host = host[..colon];

        if (!host.EndsWith(_suffix, StringComparison.Ordinal))
            return null;

        var label = host[..^_suffix.Length];

        // Exactly one label between the prefix and the apex. Rejecting a dot here is
        // what keeps this first-level: "pos-bmd.evil.spacecode.tech" must not resolve.
        if (label.Length == 0 || label.Contains('.'))
            return null;

        if (Prefix.Length > 0 && !label.StartsWith(Prefix, StringComparison.Ordinal))
            return null;

        var code = label[Prefix.Length..];

        // The host is attacker-controllable (it is just a header), so the extracted
        // code goes through the same allowlist as everything else before it is used
        // to look up — or worse, provision — a database.
        return PgIdentifier.IsValidCode(code) ? code : null;
    }

    /// <summary>The canonical hostname for a business code. Used to show operators the shop's URL.</summary>
    public string HostForCode(string code) => $"{Prefix}{PgIdentifier.RequireCode(code)}.{BaseHost}";

    /// <summary>The canonical https URL for a business code.</summary>
    public string UrlForCode(string code) => $"https://{HostForCode(code)}/";
}
