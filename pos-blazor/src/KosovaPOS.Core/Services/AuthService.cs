using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Models.BMDData;
using KosovaPOS.Models.Control;

namespace KosovaPOS.Core.Services;

/// <summary>
/// Credential verification against a business's POSUsers table.
/// Ported from POS2/Windows/LoginWindow.xaml.cs (BCrypt with legacy plain-text
/// fallback) into a platform-agnostic, injectable service.
///
/// Takes the <see cref="Business"/> as an argument rather than resolving it from
/// the ambient user: at login there is no signed-in user yet, so there is no
/// business to resolve from. This is the one service that must not use the
/// tenant-aware DbContext factory.
/// </summary>
public class AuthService
{
    private readonly PosDbContextFactory _posFactory;

    public AuthService(PosDbContextFactory posFactory) => _posFactory = posFactory;

    /// <summary>
    /// Verifies a username/password pair within one business. Returns the active
    /// user on success, otherwise null. Uses BCrypt, falling back to plain-text
    /// for legacy rows.
    /// </summary>
    public async Task<POSUser?> AuthenticateAsync(Business business, string username, string password)
    {
        ArgumentNullException.ThrowIfNull(business);

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return null;

        await using var db = _posFactory.CreateFor(business);
        var user = await db.POSUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username);

        if (user is null || !user.IsActive)
            return null;

        bool valid;
        try
        {
            valid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        }
        catch
        {
            // Legacy plain-text password fallback (same behaviour as desktop)
            valid = user.PasswordHash == password;
        }

        return valid ? user : null;
    }

    public const string FullNameClaim = "full_name";
    public const string UserIdClaim = "uid";
    public const string PermissionClaim = "perm";

    /// <summary>
    /// The business this session is bound to. Everything the user can reach is
    /// decided by this claim: the DbContext factory turns it into a database name,
    /// and a session without it can open no business database at all.
    /// </summary>
    public const string BusinessIdClaim = "bizid";
    public const string BusinessCodeClaim = "bizcode";
    public const string BusinessNameClaim = "bizname";

    /// <summary>Marks a platform operator. Mutually exclusive with <see cref="BusinessIdClaim"/>.</summary>
    public const string PlatformAdminClaim = "platform";

    /// <summary>Authorization policy name guarding the /admin area.</summary>
    public const string PlatformPolicy = "platform-admin";

    /// <summary>
    /// Authorization policy requiring the session to be bound to a business.
    /// Guards every page that reads business data but needs no finer permission
    /// (the home dashboard); the rest are already behind a "perm:*" policy.
    /// </summary>
    public const string BusinessPolicy = "business";

    /// <summary>
    /// Every permission <see cref="HasPermission"/> understands. Startup turns each
    /// into a "perm:{name}" authorization policy, so a permission added here is
    /// gateable on a page without touching Program.cs.
    /// </summary>
    public static readonly string[] AllPermissions =
        ["reports", "purchases", "articles", "users", "settings", "manager", "finance", "partners",
         "sell", "stock"];

    /// <summary>
    /// Builds the claims identity stored in the auth cookie for a signed-in user,
    /// including a claim per granted permission so components can gate UI cheaply,
    /// and the business the session is pinned to.
    /// </summary>
    public static ClaimsPrincipal BuildPrincipal(POSUser user, Business business, string authScheme)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new(FullNameClaim, user.FullName),
            new(UserIdClaim, user.Id.ToString()),
            new(ClaimTypes.Role, user.Role),
            new(BusinessIdClaim, business.Id.ToString()),
            new(BusinessCodeClaim, business.Code),
            new(BusinessNameClaim, business.Name),
        };

        foreach (var perm in AllPermissions)
        {
            if (HasPermission(user, perm))
                claims.Add(new Claim(PermissionClaim, perm));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authScheme));
    }

    /// <summary>
    /// Identity for a platform operator. Carries no business and no POS permissions,
    /// so every business page and every business query is closed to it.
    /// </summary>
    public static ClaimsPrincipal BuildPlatformPrincipal(PlatformAdmin admin, string authScheme)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, admin.Username),
            new(FullNameClaim, admin.FullName),
            new(UserIdClaim, admin.Id.ToString()),
            new(PlatformAdminClaim, "true"),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authScheme));
    }

    /// <summary>
    /// Permission check ported from LoginWindow.HasPermissionStatic.
    /// </summary>
    public static bool HasPermission(POSUser user, string permission)
    {
        if (user.Role == "Admin")
            return true;

        return permission.ToLowerInvariant() switch
        {
            "finance"   => user.CanViewReports,
            "reports"   => user.CanViewReports,
            "purchases" => user.CanManagePurchases,
            "articles"  => user.CanManageArticles,
            "partners"  => user.CanManagePurchases,
            "settings"  => user.Role == "Manager",
            "sell"      => user.CanSell,
            "stock"     => user.CanManageStock,
            "users"     => user.CanManageUsers,
            "manager"   => user.Role == "Manager" || user.CanDeleteReceipts,
            _           => false
        };
    }
}
