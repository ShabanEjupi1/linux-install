using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Models.BMDData;

namespace KosovaPOS.Core.Services;

/// <summary>
/// CRUD over the <see cref="POSUser"/> table — the same table the login uses
/// (see <see cref="AuthService"/>). Ported from POS2/Windows/UsersWindow +
/// UserEditWindow. Passwords are BCrypt-hashed here so the UI never touches hashes.
/// </summary>
public class UserService
{
    private readonly IDbContextFactory<PosDbContext> _dbFactory;

    public UserService(IDbContextFactory<PosDbContext> dbFactory) => _dbFactory = dbFactory;

    /// <summary>Known roles, mirrors the desktop UserEditWindow role list.</summary>
    public static readonly string[] Roles =
        { "Admin", "Manager", "Cashier", "Warehouse", "Accountant" };

    public async Task<List<POSUser>> GetAllAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.POSUsers.AsNoTracking()
            .OrderByDescending(u => u.IsActive)
            .ThenBy(u => u.Username)
            .ToListAsync();
    }

    public async Task<POSUser?> FindByIdAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.POSUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
    }

    /// <summary>
    /// The signed-in user's name <b>as it is now</b> — read from the database, not from the
    /// login cookie.
    ///
    /// The cookie's <c>full_name</c> claim is a photograph taken at sign-in: rename a user and
    /// every receipt they print keeps their old name on it until they happen to log out, and
    /// rename someone ELSE and their till keeps the old name for as long as their session lasts.
    /// Anything that puts a person's name on paper or in the journal must ask the database,
    /// which costs one indexed row read next to a sale that writes several.
    ///
    /// Falls back to the claim if the row is gone — a receipt with a stale name still beats a
    /// receipt with no cashier at all.
    /// </summary>
    public async Task<string> CurrentFullNameAsync(ClaimsPrincipal? principal)
    {
        var claimed = principal?.FindFirst(AuthService.FullNameClaim)?.Value
                      ?? principal?.Identity?.Name ?? "";

        if (!int.TryParse(principal?.FindFirst(AuthService.UserIdClaim)?.Value, out var id) || id <= 0)
            return claimed;

        var user = await FindByIdAsync(id);
        return string.IsNullOrWhiteSpace(user?.FullName) ? claimed : user!.FullName!;
    }

    /// <summary>True if the username is taken by a *different* user.</summary>
    public async Task<bool> UsernameTakenAsync(string username, int excludeId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.POSUsers.AsNoTracking()
            .AnyAsync(u => u.Username == username && u.Id != excludeId);
    }

    /// <summary>
    /// Insert or update a user. When <paramref name="newPassword"/> is non-empty it
    /// is BCrypt-hashed and stored; on insert it is required. Returns the user id.
    /// </summary>
    public async Task<int> SaveAsync(POSUser user, string? newPassword)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var row = user.Id > 0
            ? await db.POSUsers.FirstOrDefaultAsync(u => u.Id == user.Id)
            : null;

        if (row is null)
        {
            if (string.IsNullOrEmpty(newPassword))
                throw new InvalidOperationException("Fjalëkalimi është i detyrueshëm për një përdorues të ri.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.CreatedAt = DateTime.Now;
            db.POSUsers.Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        }

        row.Username = user.Username;
        row.FullName = user.FullName;
        row.Email = user.Email;
        row.Role = user.Role;
        row.IsActive = user.IsActive;
        row.Branch = user.Branch;
        row.PhoneNumber = user.PhoneNumber;
        row.CanSell = user.CanSell;
        row.CanManageStock = user.CanManageStock;
        row.CanManageArticles = user.CanManageArticles;
        row.CanManagePurchases = user.CanManagePurchases;
        row.CanManageUsers = user.CanManageUsers;
        row.CanViewReports = user.CanViewReports;
        row.CanModifyPrices = user.CanModifyPrices;
        row.CanDeleteReceipts = user.CanDeleteReceipts;
        row.CanGiveDiscounts = user.CanGiveDiscounts;
        row.MaxDiscountPercent = user.MaxDiscountPercent;

        if (!string.IsNullOrEmpty(newPassword))
            row.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

        await db.SaveChangesAsync();
        return row.Id;
    }

    /// <summary>
    /// Deactivate a user (soft delete) — safer than a hard delete because journal
    /// rows reference cashiers. Refuses to disable the last active Admin.
    /// </summary>
    public async Task<(bool ok, string? error)> DeactivateAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.POSUsers.FirstOrDefaultAsync(u => u.Id == id);
        if (row is null) return (false, "Përdoruesi nuk u gjet.");

        if (row.Role == "Admin" && row.IsActive)
        {
            var otherAdmins = await db.POSUsers
                .CountAsync(u => u.Role == "Admin" && u.IsActive && u.Id != id);
            if (otherAdmins == 0)
                return (false, "Nuk mund të çaktivizohet administratori i fundit aktiv.");
        }

        row.IsActive = false;
        await db.SaveChangesAsync();
        return (true, null);
    }
}
