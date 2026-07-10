using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Models.BMDData;

namespace KosovaPOS.Core.Services;

/// <summary>
/// Credential verification against the POSUsers table.
/// Ported from POS2/Windows/LoginWindow.xaml.cs (BCrypt with legacy plain-text
/// fallback) into a platform-agnostic, injectable service.
/// </summary>
public class AuthService
{
    private readonly IDbContextFactory<PosDbContext> _dbFactory;

    public AuthService(IDbContextFactory<PosDbContext> dbFactory) => _dbFactory = dbFactory;

    /// <summary>
    /// Verifies a username/password pair. Returns the active user on success,
    /// otherwise null. Uses BCrypt, falling back to plain-text for legacy rows.
    /// </summary>
    public async Task<POSUser?> AuthenticateAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync();
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
    /// Every permission <see cref="HasPermission"/> understands. Startup turns each
    /// into a "perm:{name}" authorization policy, so a permission added here is
    /// gateable on a page without touching Program.cs.
    /// </summary>
    public static readonly string[] AllPermissions =
        ["reports", "purchases", "articles", "users", "settings", "manager", "finance", "partners"];

    /// <summary>
    /// Builds the claims identity stored in the auth cookie for a signed-in user,
    /// including a claim per granted permission so components can gate UI cheaply.
    /// </summary>
    public static ClaimsPrincipal BuildPrincipal(POSUser user, string authScheme)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new(FullNameClaim, user.FullName),
            new(UserIdClaim, user.Id.ToString()),
            new(ClaimTypes.Role, user.Role),
        };

        foreach (var perm in AllPermissions)
        {
            if (HasPermission(user, perm))
                claims.Add(new Claim(PermissionClaim, perm));
        }

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
            "users"     => user.CanManageUsers,
            "manager"   => user.Role == "Manager" || user.CanDeleteReceipts,
            _           => false
        };
    }
}
