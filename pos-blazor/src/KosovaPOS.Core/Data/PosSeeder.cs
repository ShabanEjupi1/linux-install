using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using KosovaPOS.Models;
using KosovaPOS.Models.BMDData;

namespace KosovaPOS.Core.Data;

/// <summary>
/// First-run seeding for a business database. Shared by startup (for the primary
/// business) and by the provisioner (for every business created afterwards) so
/// the two cannot drift into seeding different things.
/// </summary>
public static class PosSeeder
{
    /// <summary>
    /// Adds an Admin user if the database has no users at all. Returns the
    /// generated password when it invented one, null when the caller supplied a
    /// password or when users already existed.
    ///
    /// There is deliberately no well-known default password: an unattended first
    /// run produces a random one that the operator has to go and read.
    /// </summary>
    public static async Task<string?> EnsureAdminAsync(
        PosDbContext db,
        string username,
        string fullName,
        string? password,
        CancellationToken ct = default)
    {
        if (await db.POSUsers.AnyAsync(ct))
            return null;

        var generated = string.IsNullOrEmpty(password);
        password ??= Convert.ToBase64String(RandomNumberGenerator.GetBytes(12));

        db.POSUsers.Add(new POSUser
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FullName = fullName,
            Role = "Admin",
            IsActive = true,
            CanSell = true,
            CanManageStock = true,
            CanManageArticles = true,
            CanManagePurchases = true,
            CanManageUsers = true,
            CanViewReports = true,
            CanModifyPrices = true,
            CanDeleteReceipts = true,
            CanGiveDiscounts = true,
        });
        await db.SaveChangesAsync(ct);

        return generated ? password : null;
    }

    /// <summary>
    /// Writes the business name into the singleton BusinessSettings row of a fresh
    /// database so the shell has something to show before anyone opens Cilësimet.
    /// Leaves IsFirstRun set — the owner still walks through setup.
    /// </summary>
    public static async Task EnsureBusinessSettingsAsync(PosDbContext db, string businessName, CancellationToken ct = default)
    {
        if (await db.BusinessSettings.AnyAsync(ct))
            return;

        db.BusinessSettings.Add(new BusinessSettings { BusinessName = businessName, IsFirstRun = true });
        await db.SaveChangesAsync(ct);
    }
}
