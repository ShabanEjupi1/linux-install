using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace KosovaPOS.Web.Services;

/// <summary>
/// Feeds the Blazor circuit its <see cref="AuthenticationState"/> from the
/// authenticated cookie. The scoped instance is constructed while the initial
/// HTTP request (that establishes the SignalR circuit) is still in flight, so
/// <c>HttpContext.User</c> — populated by the cookie middleware — is available
/// and captured for the circuit's lifetime.
/// </summary>
public class PosAuthStateProvider : AuthenticationStateProvider
{
    private readonly Task<AuthenticationState> _state;

    public PosAuthStateProvider(IHttpContextAccessor accessor)
    {
        var principal = accessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
        _state = Task.FromResult(new AuthenticationState(principal));
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _state;
}
