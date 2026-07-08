using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Models;

namespace KosovaPOS.Core.Services;

/// <summary>
/// Loads the installation's <see cref="BusinessSettings"/> — the single record
/// that carries the tenant <see cref="BusinessProfile"/> (Retail / FoodAndBeverage
/// / …) and the Enable* feature toggles that drive which UI a tenant sees.
/// This is the "tenant mode" abstraction: the shared app reads it to decide
/// between restaurant-style and store-style behaviour.
/// </summary>
public class TenantService
{
    private readonly IDbContextFactory<PosDbContext> _dbFactory;

    public TenantService(IDbContextFactory<PosDbContext> dbFactory) => _dbFactory = dbFactory;

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

    /// <summary>True when the tenant runs in restaurant / food-service mode.</summary>
    public async Task<bool> IsRestaurantAsync()
    {
        var s = await GetSettingsAsync();
        return s.Profile == BusinessProfile.FoodAndBeverage
            || s.Profile == BusinessProfile.Hospitality
            || s.EnableTableManagement
            || s.EnableKitchenDisplay;
    }
}
