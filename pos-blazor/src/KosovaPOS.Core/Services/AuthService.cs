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

    /// <summary>
    /// The users of one business, for the platform impersonation console. Uses the
    /// non-tenant factory with an explicit business because the caller is a platform
    /// admin, who has no business in scope — the tenant-aware factory would throw.
    /// </summary>
    public async Task<List<POSUser>> ListUsersAsync(Business business)
    {
        ArgumentNullException.ThrowIfNull(business);
        await using var db = _posFactory.CreateFor(business);
        return await db.POSUsers.AsNoTracking()
            .OrderByDescending(u => u.IsActive).ThenBy(u => u.Username)
            .ToListAsync();
    }

    /// <summary>One user of one business by id, for impersonation. See <see cref="ListUsersAsync"/>.</summary>
    public async Task<POSUser?> FindUserAsync(Business business, int userId)
    {
        ArgumentNullException.ThrowIfNull(business);
        await using var db = _posFactory.CreateFor(business);
        return await db.POSUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
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

    /// <summary>
    /// Present only while a platform admin is impersonating a business user. Holds
    /// the platform operator's username, name and id so the banner can name who is
    /// really driving and "exit impersonation" can restore the platform session
    /// without a second login. Its presence is what the impersonation banner keys on.
    /// </summary>
    public const string ImpersonatorClaim = "impersonator";
    public const string ImpersonatorNameClaim = "impersonator_name";
    public const string ImpersonatorIdClaim = "impersonator_uid";

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
         "sell", "stock", "audit"];

    /// <summary>
    /// Builds the claims identity stored in the auth cookie for a signed-in user,
    /// including a claim per granted permission so components can gate UI cheaply,
    /// and the business the session is pinned to.
    /// </summary>
    public static ClaimsPrincipal BuildPrincipal(POSUser user, Business business, string authScheme,
        ClaimsPrincipal? impersonator = null)
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

        // Stamp who is really driving, so the session is a business user for every
        // gate and query (the point of impersonation) yet still auditable and
        // reversible. Only honoured from a genuine platform-admin principal.
        if (impersonator is not null && impersonator.HasClaim(PlatformAdminClaim, "true"))
        {
            claims.Add(new Claim(ImpersonatorClaim, impersonator.Identity?.Name ?? ""));
            claims.Add(new Claim(ImpersonatorNameClaim,
                impersonator.FindFirst(FullNameClaim)?.Value ?? impersonator.Identity?.Name ?? ""));
            claims.Add(new Claim(ImpersonatorIdClaim, impersonator.FindFirst(UserIdClaim)?.Value ?? ""));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authScheme));
    }

    /// <summary>
    /// Rebuilds a platform-admin principal from the impersonator claims carried by
    /// an impersonating session, so "exit impersonation" restores the operator
    /// without a second password prompt. Returns null if the principal is not
    /// actually impersonating (no impersonator claim).
    /// </summary>
    public static ClaimsPrincipal? BuildPrincipalFromImpersonator(ClaimsPrincipal impersonating, string authScheme)
    {
        var username = impersonating.FindFirst(ImpersonatorClaim)?.Value;
        if (string.IsNullOrEmpty(username))
            return null;

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new(FullNameClaim, impersonating.FindFirst(ImpersonatorNameClaim)?.Value ?? username),
            new(UserIdClaim, impersonating.FindFirst(ImpersonatorIdClaim)?.Value ?? ""),
            new(PlatformAdminClaim, "true"),
        };
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

            // Who is allowed to read the audit trail. Deliberately narrower than
            // "users": someone who can create cashiers should not automatically be
            // able to read what everyone in the shop has been doing, and the log is
            // the one screen a dishonest manager would most want to see (and, once
            // it can be seen, to argue about). Manager or Admin only.
            "audit"     => user.Role == "Manager",
            _           => false
        };
    }
}
