using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Models.Control;

namespace KosovaPOS.Core.Services;

/// <summary>
/// Reads the business registry out of the control database, with a short-lived
/// in-memory snapshot.
///
/// The cache is not an optimisation detail — it is on the path of *every single
/// query the app makes*, because the tenant-aware DbContext factory resolves a
/// business before it can open a connection. Hitting the control database for
/// each of those would double the query count of the whole application.
///
/// Only active businesses are cached. Clearing <see cref="Business.IsActive"/>
/// therefore locks a business out within <see cref="Ttl"/>, without waiting for
/// its users' 12-hour auth cookies to expire.
/// </summary>
public sealed class BusinessRegistry
{
    /// <summary>
    /// Short enough that deactivating a business takes effect while you are still
    /// looking at the screen; long enough that a busy till isn't querying the
    /// control database on every keystroke.
    /// </summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private readonly IDbContextFactory<ControlDbContext> _controlFactory;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private sealed record Snapshot(
        IReadOnlyDictionary<int, Business> ById,
        IReadOnlyDictionary<string, Business> ByCode,
        IReadOnlyDictionary<string, Business> ByShopDomain,
        DateTimeOffset LoadedAt);

    private volatile Snapshot? _snapshot;

    public BusinessRegistry(IDbContextFactory<ControlDbContext> controlFactory, TimeProvider? clock = null)
    {
        _controlFactory = controlFactory;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Drop the cache. Call after any write to the registry.</summary>
    public void Invalidate() => _snapshot = null;

    private async Task<Snapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        var current = _snapshot;
        if (current is not null && _clock.GetUtcNow() - current.LoadedAt < Ttl)
            return current;

        await _refreshLock.WaitAsync(ct);
        try
        {
            // Another caller may have refreshed while we waited on the lock.
            current = _snapshot;
            if (current is not null && _clock.GetUtcNow() - current.LoadedAt < Ttl)
                return current;

            await using var db = await _controlFactory.CreateDbContextAsync(ct);
            var active = await db.Businesses.AsNoTracking()
                .Where(b => b.IsActive)
                .ToListAsync(ct);

            var snapshot = new Snapshot(
                active.ToDictionary(b => b.Id),
                active.ToDictionary(b => b.Code, StringComparer.Ordinal),
                active.Where(b => !string.IsNullOrWhiteSpace(b.ShopDomain))
                      .ToDictionary(b => b.ShopDomain!.Trim().ToLowerInvariant(), StringComparer.Ordinal),
                _clock.GetUtcNow());

            _snapshot = snapshot;
            return snapshot;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>The active business with this id, or null if it is unknown or deactivated.</summary>
    public async Task<Business?> GetActiveByIdAsync(int id, CancellationToken ct = default)
        => (await GetSnapshotAsync(ct)).ById.GetValueOrDefault(id);

    /// <summary>
    /// The active business for a login code, or null. Codes are stored lowercase;
    /// the caller's input is normalised here so "BMD" and " bmd " both resolve.
    /// </summary>
    public async Task<Business?> GetActiveByCodeAsync(string? code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return (await GetSnapshotAsync(ct)).ByCode.GetValueOrDefault(code.Trim().ToLowerInvariant());
    }

    /// <summary>
    /// The active business whose public shop lives on this hostname, or null.
    ///
    /// The argument is a raw <c>Host</c> header, so it may carry a port and any casing,
    /// and it is attacker-controllable. That is fine: the only thing a forged Host can
    /// select is *which shop's public catalogue* is rendered — every page that reads
    /// anything private is behind an authorization policy, and the policies check the
    /// signed <c>bizid</c> claim, never the host. A lie here buys a view of a shop's
    /// storefront, which is public by definition.
    ///
    /// A "www." prefix resolves to the same business, because a customer typing
    /// www.enisi.tech is not a different customer.
    /// </summary>
    public async Task<Business?> GetActiveByShopDomainAsync(string? host, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(host)) return null;

        host = host.Trim().ToLowerInvariant();
        var colon = host.IndexOf(':');
        if (colon >= 0) host = host[..colon];

        var byDomain = (await GetSnapshotAsync(ct)).ByShopDomain;

        if (byDomain.TryGetValue(host, out var direct)) return direct;
        if (host.StartsWith("www.", StringComparison.Ordinal) &&
            byDomain.TryGetValue(host[4..], out var bare)) return bare;

        return null;
    }

    /// <summary>Active businesses, for startup migration sweeps.</summary>
    public async Task<IReadOnlyList<Business>> ListActiveAsync(CancellationToken ct = default)
        => (await GetSnapshotAsync(ct)).ById.Values.OrderBy(b => b.Name).ToList();

    /// <summary>
    /// Every business including deactivated ones, read straight from the control
    /// database. For the admin console, which must show what the cache hides.
    /// </summary>
    public async Task<IReadOnlyList<Business>> ListAllUncachedAsync(CancellationToken ct = default)
    {
        await using var db = await _controlFactory.CreateDbContextAsync(ct);
        return await db.Businesses.AsNoTracking().OrderBy(b => b.Name).ToListAsync(ct);
    }

    /// <summary>
    /// Point a business's public shop at a hostname, or clear it. Normalised to bare
    /// lowercase (no scheme, no "www.", no trailing slash) so the lookup above — which
    /// sees whatever a browser puts in the Host header — can match on equality.
    /// </summary>
    public async Task SetShopDomainAsync(int id, string? domain, CancellationToken ct = default)
    {
        var normalised = NormaliseShopDomain(domain);

        await using var db = await _controlFactory.CreateDbContextAsync(ct);
        var row = await db.Businesses.FirstOrDefaultAsync(b => b.Id == id, ct)
                  ?? throw new InvalidOperationException($"No business with id {id}.");
        row.ShopDomain = normalised;
        await db.SaveChangesAsync(ct);
        Invalidate();
    }

    public static string? NormaliseShopDomain(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return null;

        var value = domain.Trim().ToLowerInvariant();
        foreach (var scheme in (string[])["https://", "http://"])
            if (value.StartsWith(scheme, StringComparison.Ordinal))
                value = value[scheme.Length..];

        value = value.TrimEnd('/');
        var slash = value.IndexOf('/');
        if (slash >= 0) value = value[..slash];

        if (value.StartsWith("www.", StringComparison.Ordinal))
            value = value[4..];

        return value.Length == 0 ? null : value;
    }

    public async Task SetActiveAsync(int id, bool isActive, CancellationToken ct = default)
    {
        await using var db = await _controlFactory.CreateDbContextAsync(ct);
        var row = await db.Businesses.FirstOrDefaultAsync(b => b.Id == id, ct)
                  ?? throw new InvalidOperationException($"No business with id {id}.");
        row.IsActive = isActive;
        await db.SaveChangesAsync(ct);
        Invalidate();
    }
}
