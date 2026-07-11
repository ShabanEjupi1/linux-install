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
