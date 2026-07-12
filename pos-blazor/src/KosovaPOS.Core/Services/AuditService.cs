using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using KosovaPOS.Core.Data;
using KosovaPOS.Models;
using KosovaPOS.Models.Control;

namespace KosovaPOS.Core.Services;

/// <summary>Who performed an audited act. Built by the Web layer from the signed-in principal.</summary>
/// <param name="UserId">The POSUser id, as text — audit rows must outlive the user row.</param>
/// <param name="ImpersonatedBy">The platform operator, when one is driving this session.</param>
public sealed record AuditActor(string UserId, string UserName, string? ImpersonatedBy = null, string? IpAddress = null)
{
    /// <summary>The actor for acts that happen before anyone is signed in (a failed login).</summary>
    public static AuditActor Anonymous(string attemptedUsername, string? ip) =>
        new("0", attemptedUsername, null, ip);
}

/// <summary>The verbs the log uses. Kept as constants so the filter dropdown and the writers cannot drift.</summary>
public static class AuditAction
{
    public const string Login = "LOGIN";
    public const string LoginFailed = "LOGIN_FAILED";
    public const string Lockout = "LOCKOUT";
    public const string Logout = "LOGOUT";
    public const string ImpersonateStart = "IMPERSONATE_START";
    public const string ImpersonateEnd = "IMPERSONATE_END";
    public const string Sale = "SALE";
    public const string Return = "RETURN";
    public const string ShiftOpen = "SHIFT_OPEN";
    public const string ShiftClose = "SHIFT_CLOSE";
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Delete = "DELETE";
    public const string StockAdjust = "STOCK_ADJUST";
    public const string BusinessCreate = "BUSINESS_CREATE";
    public const string BusinessUpdate = "BUSINESS_UPDATE";

    /// <summary>Albanian labels for the audit screens. Unknown verbs fall back to themselves.</summary>
    public static string Label(string action) => action switch
    {
        Login             => "Kyçje",
        LoginFailed       => "Kyçje e dështuar",
        Lockout           => "Bllokim",
        Logout            => "Dalje",
        ImpersonateStart  => "Imitim — fillim",
        ImpersonateEnd    => "Imitim — fund",
        Sale              => "Shitje",
        Return            => "Kthim",
        ShiftOpen         => "Hapje ndërrimi",
        ShiftClose        => "Mbyllje ndërrimi",
        Create            => "Krijim",
        Update            => "Ndryshim",
        Delete            => "Fshirje",
        StockAdjust       => "Korrigjim stoku",
        BusinessCreate    => "Biznes i ri",
        BusinessUpdate    => "Ndryshim biznesi",
        _                 => action,
    };
}

/// <summary>
/// Writes and reads the audit trail.
///
/// Two logs, on purpose. Acts inside a shop go to that shop's own database, so the
/// evidence lives with the data it describes and a shop can be handed its own
/// history. Acts by a platform operator go to the control database, because they
/// either touch no business database (a platform login) or must survive one being
/// deleted. Impersonation is written to both.
///
/// Takes the <see cref="Business"/> as an argument rather than resolving it from the
/// ambient session, for the same reason <see cref="AuthService"/> does: the two most
/// important things it records — a failed login and a lockout — happen when there is
/// no signed-in user and therefore no tenant-aware DbContext to reach for.
///
/// <b>Writes are best-effort.</b> A failed audit write is logged and swallowed rather
/// than thrown, because the alternative is that a Postgres hiccup in the audit table
/// refuses a customer's cash. That is a deliberate trade: this log is an investigative
/// record, not a fiscal ledger, and it must never be the reason a shop cannot sell. If
/// it is ever promoted to evidence the tax authority relies on, the sale and its audit
/// row have to share one transaction — which they do not today.
/// </summary>
public sealed class AuditService
{
    private readonly PosDbContextFactory _posFactory;
    private readonly IDbContextFactory<ControlDbContext> _controlFactory;
    private readonly ILogger<AuditService> _log;

    public AuditService(PosDbContextFactory posFactory,
                        IDbContextFactory<ControlDbContext> controlFactory,
                        ILogger<AuditService> log)
    {
        _posFactory = posFactory;
        _controlFactory = controlFactory;
        _log = log;
    }

    /// <summary>Appends one row to a business's log.</summary>
    public async Task WriteAsync(Business business, AuditActor actor, string action, string entityType,
        int? entityId = null, string? entityName = null, string? details = null,
        string? oldValue = null, string? newValue = null, bool success = true, string? errorMessage = null)
    {
        ArgumentNullException.ThrowIfNull(business);

        var row = new AuditLog
        {
            Timestamp = DateTime.Now,
            UserId = Trim(actor.UserId, 100),
            UserName = Trim(actor.UserName, 100),
            ImpersonatedBy = Trim(actor.ImpersonatedBy, 100),
            IpAddress = Trim(actor.IpAddress, 200),
            Action = Trim(action, 50)!,
            EntityType = Trim(entityType, 100)!,
            EntityId = entityId,
            EntityName = Trim(entityName, 500),
            Details = Trim(details, 500),
            OldValue = oldValue,
            NewValue = newValue,
            IsSuccess = success,
            ErrorMessage = Trim(errorMessage, 1000),
        };

        try
        {
            await using var db = _posFactory.CreateFor(business);
            db.AuditLogs.Add(row);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Audit write failed for {Business}: {Action} {EntityType} by {User}",
                business.Code, action, entityType, actor.UserName);
        }
    }

    /// <summary>Appends one row to the platform log in the control database.</summary>
    public async Task WritePlatformAsync(string actor, string action, Business? business = null,
        string? targetUser = null, string? ip = null, string? details = null, bool success = true)
    {
        var row = new PlatformAuditLog
        {
            Timestamp = DateTime.Now,
            Actor = Trim(actor, 100)!,
            Action = Trim(action, 50)!,
            BusinessId = business?.Id,
            BusinessCode = Trim(business?.Code, 32),
            TargetUser = Trim(targetUser, 100),
            IpAddress = Trim(ip, 200),
            Details = Trim(details, 500),
            IsSuccess = success,
        };

        try
        {
            await using var db = await _controlFactory.CreateDbContextAsync();
            db.PlatformAuditLogs.Add(row);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Platform audit write failed: {Action} by {Actor}", action, actor);
        }
    }

    /// <summary>One business's log, newest first.</summary>
    public async Task<List<AuditLog>> ReadAsync(Business business, DateTime? from = null, DateTime? to = null,
        string? action = null, string? username = null, int take = 300)
    {
        ArgumentNullException.ThrowIfNull(business);

        await using var db = _posFactory.CreateFor(business);
        var q = db.AuditLogs.AsNoTracking().AsQueryable();

        if (from is not null)
            q = q.Where(a => a.Timestamp >= from.Value.Date);
        if (to is not null)
            q = q.Where(a => a.Timestamp < to.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(action))
            q = q.Where(a => a.Action == action);
        if (!string.IsNullOrWhiteSpace(username))
            q = q.Where(a => a.UserName == username);

        return await q.OrderByDescending(a => a.Timestamp).Take(take).ToListAsync();
    }

    /// <summary>The distinct usernames that appear in a business's log, for the filter.</summary>
    public async Task<List<string>> ReadUsersAsync(Business business)
    {
        ArgumentNullException.ThrowIfNull(business);
        await using var db = _posFactory.CreateFor(business);
        return await db.AuditLogs.AsNoTracking()
            .Select(a => a.UserName).Distinct().OrderBy(u => u).ToListAsync();
    }

    /// <summary>The platform log, newest first.</summary>
    public async Task<List<PlatformAuditLog>> ReadPlatformAsync(DateTime? from = null, DateTime? to = null,
        string? action = null, int? businessId = null, int take = 300)
    {
        await using var db = await _controlFactory.CreateDbContextAsync();
        var q = db.PlatformAuditLogs.AsNoTracking().AsQueryable();

        if (from is not null)
            q = q.Where(a => a.Timestamp >= from.Value.Date);
        if (to is not null)
            q = q.Where(a => a.Timestamp < to.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(action))
            q = q.Where(a => a.Action == action);
        if (businessId is not null)
            q = q.Where(a => a.BusinessId == businessId);

        return await q.OrderByDescending(a => a.Timestamp).Take(take).ToListAsync();
    }

    /// <summary>
    /// Audit columns are narrow and their inputs are not (a Postgres value too long
    /// for varchar(n) is an error, not a truncation). A clipped detail is worth more
    /// than a lost row.
    /// </summary>
    private static string? Trim(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
