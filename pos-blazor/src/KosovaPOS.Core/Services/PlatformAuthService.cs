using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using KosovaPOS.Core.Data;
using KosovaPOS.Models.Control;

namespace KosovaPOS.Core.Services;

/// <summary>
/// Credential verification for platform operators, against the control database.
/// Separate from <see cref="AuthService"/> because it authenticates a different
/// principal against a different database with a different blast radius: a
/// platform admin can create businesses, so this login is the front door to the
/// whole platform, not to one shop.
/// </summary>
public class PlatformAuthService
{
    private readonly IDbContextFactory<ControlDbContext> _controlFactory;

    public PlatformAuthService(IDbContextFactory<ControlDbContext> controlFactory)
        => _controlFactory = controlFactory;

    public async Task<PlatformAdmin?> AuthenticateAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return null;

        await using var db = await _controlFactory.CreateDbContextAsync();
        var admin = await db.PlatformAdmins.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Username == username);

        if (admin is null || !admin.IsActive)
            return null;

        // No plain-text fallback here, unlike the POS users: there are no legacy
        // platform-admin rows to be compatible with. A row that isn't a valid
        // BCrypt hash fails closed.
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash) ? admin : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Seeds the first platform admin if none exists. Returns the generated
    /// password when it invented one. Mirrors <see cref="PosSeeder.EnsureAdminAsync"/>.
    /// </summary>
    public static async Task<string?> EnsureSeedAdminAsync(
        ControlDbContext db, string username, string fullName, string? password, CancellationToken ct = default)
    {
        if (await db.PlatformAdmins.AnyAsync(ct))
            return null;

        var generated = string.IsNullOrEmpty(password);
        password ??= Convert.ToBase64String(RandomNumberGenerator.GetBytes(12));

        db.PlatformAdmins.Add(new PlatformAdmin
        {
            Username = username,
            FullName = fullName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            IsActive = true,
        });
        await db.SaveChangesAsync(ct);

        return generated ? password : null;
    }
}
