using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using KosovaPOS.Core.Services;
using KosovaPOS.Models;
using KosovaPOS.Models.Control;

namespace KosovaPOS.Web.Services;

/// <summary>
/// The audit trail as a component sees it: <c>await Audit.LogAsync(AuditAction.Sale, "Receipt", id, number)</c>.
///
/// Exists so that call sites never assemble an <see cref="AuditActor"/> by hand. Who
/// is acting, which business they are in, whether a platform operator is driving, and
/// what address they came from are all session facts — a page that had to pass them in
/// would eventually pass the wrong ones, and an audit trail that can be handed the
/// wrong actor is worse than none.
///
/// The client address is captured in the constructor, while the circuit's initial HTTP
/// request is still in flight — the same window <see cref="PosAuthStateProvider"/>
/// relies on. By the time a component method runs, <c>HttpContext</c> is long gone and
/// an <c>IHttpContextAccessor</c> read would return null.
/// </summary>
public sealed class Audit
{
    private readonly AuditService _audit;
    private readonly CurrentBusiness _business;
    private readonly AuthenticationStateProvider _authState;
    private readonly string? _ip;

    public Audit(AuditService audit, CurrentBusiness business, AuthenticationStateProvider authState,
                 IHttpContextAccessor accessor)
    {
        _audit = audit;
        _business = business;
        _authState = authState;
        _ip = accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>The address the circuit was opened from, for writers that have no session (the login form).</summary>
    public string? IpAddress => _ip;

    /// <summary>
    /// Appends a row to the current business's log. Silently does nothing if the
    /// session has no business — a platform admin's acts belong in the platform log,
    /// and writing them here would need a business database they are not allowed to open.
    /// </summary>
    public async Task LogAsync(string action, string entityType, int? entityId = null,
        string? entityName = null, string? details = null, string? oldValue = null, string? newValue = null)
    {
        var business = await _business.GetAsync();
        if (business is null)
            return;

        var user = (await _authState.GetAuthenticationStateAsync()).User;
        await _audit.WriteAsync(business, ActorFrom(user, _ip), action, entityType,
            entityId, entityName, details, oldValue, newValue);
    }

    /// <summary>The actor a principal represents, including the operator behind an impersonated session.</summary>
    public static AuditActor ActorFrom(ClaimsPrincipal user, string? ip) => new(
        UserId: user.FindFirst(AuthService.UserIdClaim)?.Value ?? "0",
        UserName: user.Identity?.Name ?? "?",
        ImpersonatedBy: user.FindFirst(AuthService.ImpersonatorClaim)?.Value is { Length: > 0 } op ? op : null,
        IpAddress: ip);

    /// <summary>Reads for the /auditimi screen. Scoped to the session's business by construction.</summary>
    public async Task<List<AuditLog>> ReadAsync(DateTime? from, DateTime? to, string? action, string? username)
    {
        var business = await _business.GetAsync();
        return business is null ? [] : await _audit.ReadAsync(business, from, to, action, username);
    }

    public async Task<List<string>> ReadUsersAsync()
    {
        var business = await _business.GetAsync();
        return business is null ? [] : await _audit.ReadUsersAsync(business);
    }
}
