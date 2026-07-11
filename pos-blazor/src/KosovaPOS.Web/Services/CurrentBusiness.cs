using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using KosovaPOS.Core.Services;
using KosovaPOS.Models.Control;

namespace KosovaPOS.Web.Services;

/// <summary>
/// Thrown when code that needs a business database runs in a scope that has no
/// business — an anonymous visitor, a platform admin, or a session whose business
/// has since been deactivated or deleted.
/// </summary>
public sealed class NoBusinessInScopeException : InvalidOperationException
{
    public NoBusinessInScopeException(string message) : base(message) { }
}

/// <summary>
/// The business the current session is pinned to, resolved from the <c>bizid</c>
/// claim in the auth cookie.
///
/// The claim is the source of truth rather than the URL or a header because it is
/// signed by DataProtection and therefore unforgeable, and because it is the one
/// piece of session state that survives a Blazor Server circuit:
/// <see cref="PosAuthStateProvider"/> captures the principal while the initial
/// HTTP request is still in flight, so <c>HttpContext</c> being long gone by the
/// time a component runs a query does not matter.
///
/// The claim carries only the id. The database name is looked up in
/// <see cref="BusinessRegistry"/> on every resolve, so deactivating a business
/// takes effect without waiting for its users' cookies to expire.
/// </summary>
public sealed class CurrentBusiness
{
    private readonly AuthenticationStateProvider _authState;
    private readonly BusinessRegistry _registry;

    public CurrentBusiness(AuthenticationStateProvider authState, BusinessRegistry registry)
    {
        _authState = authState;
        _registry = registry;
    }

    /// <summary>
    /// The session's business, or null if it has none.
    ///
    /// Deliberately re-resolved on every call rather than memoised for the scope.
    /// In Blazor Server a scope is the circuit, which lives for hours — caching here
    /// would mean a deactivated business kept serving every already-open browser tab
    /// until its user happened to reload. The lookup is a dictionary hit against
    /// <see cref="BusinessRegistry"/>'s snapshot, so re-resolving is nearly free, and
    /// it is what makes deactivation take effect within the registry's TTL.
    /// </summary>
    public async Task<Business?> GetAsync()
    {
        var user = (await _authState.GetAuthenticationStateAsync()).User;
        if (user.Identity?.IsAuthenticated != true)
            return null;

        var raw = user.FindFirst(AuthService.BusinessIdClaim)?.Value;
        if (!int.TryParse(raw, out var id))
            return null;   // platform admins legitimately have no business claim

        return await _registry.GetActiveByIdAsync(id);
    }

    /// <summary>
    /// The session's business, or throw. Used by the DbContext factory: refusing to
    /// open a connection is the correct outcome for a session that cannot name a
    /// business, and it must not fall back to any default database.
    /// </summary>
    public async Task<Business> RequireAsync()
    {
        var business = await GetAsync();
        if (business is null)
        {
            var user = (await _authState.GetAuthenticationStateAsync()).User;
            throw new NoBusinessInScopeException(
                user.HasClaim(AuthService.PlatformAdminClaim, "true")
                    ? "A platform admin session is not bound to a business. Impersonate a user to reach business data."
                    : "This session is not bound to an active business. Sign in again.");
        }
        return business;
    }
}
