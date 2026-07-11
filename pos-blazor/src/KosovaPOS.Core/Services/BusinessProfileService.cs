using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Models;

namespace KosovaPOS.Core.Services;

/// <summary>
/// Loads a business's <see cref="BusinessSettings"/> — the single row per business
/// database that carries its <see cref="BusinessProfile"/> (Retail /
/// FoodAndBeverage / …) and the Enable* feature toggles.
///
/// This decides which *UI* a business sees. It is not tenancy and never was: the
/// tenant boundary is the database, chosen by <see cref="BusinessRegistry"/> and
/// opened by the tenant-aware DbContext factory. This type was called
/// TenantService, which invited exactly that confusion.
/// </summary>
public class BusinessProfileService
{
    private readonly IDbContextFactory<PosDbContext> _dbFactory;

    public BusinessProfileService(IDbContextFactory<PosDbContext> dbFactory) => _dbFactory = dbFactory;

    /// <summary>
    /// Returns the current BusinessSettings, or a sensible default if the row
    /// does not exist yet (fresh install / first run).
    /// </summary>
    public async Task<BusinessSettings> GetSettingsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var settings = await db.BusinessSettings.AsNoTracking().FirstOrDefaultAsync();
        return settings ?? new BusinessSettings { Id = 0, IsFirstRun = true };
    }

    /// <summary>True when the business runs in restaurant / food-service mode.</summary>
    public async Task<bool> IsRestaurantAsync()
    {
        var s = await GetSettingsAsync();
        return s.Profile == BusinessProfile.FoodAndBeverage
            || s.Profile == BusinessProfile.Hospitality
            || s.EnableTableManagement
            || s.EnableKitchenDisplay;
    }

    /// <summary>
    /// Upserts the singleton settings row from the fields the Settings screen edits.
    /// Copies field-by-field rather than attaching <paramref name="input"/>: the form
    /// binds a handful of columns, and replacing the whole row would reset the ~20
    /// Enable* toggles it never rendered back to their CLR defaults.
    /// </summary>
    public async Task SaveSettingsAsync(BusinessSettings input)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var row = await db.BusinessSettings.FirstOrDefaultAsync();
        if (row is null)
        {
            row = new BusinessSettings();
            db.BusinessSettings.Add(row);
        }

        row.BusinessName = Clean(input.BusinessName);
        row.Address      = Clean(input.Address);
        row.FiscalNumber = Clean(input.FiscalNumber);
        row.Profile      = input.Profile;
        row.IsFirstRun   = false;

        await db.SaveChangesAsync();

        static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
