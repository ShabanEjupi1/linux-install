using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Models;

namespace KosovaPOS.Core.Services;

/// <summary>
/// CRUD over <see cref="BusinessPartner"/> — customers, suppliers, or both.
/// Ported from POS2/Windows/BusinessPartnersWindow + BusinessPartnerEditWindow.
/// </summary>
public class PartnerService
{
    private readonly IDbContextFactory<PosDbContext> _dbFactory;

    public PartnerService(IDbContextFactory<PosDbContext> dbFactory) => _dbFactory = dbFactory;

    public const string Customer = "Klient";
    public const string Supplier = "Furnizues";
    public const string Both = "Të dy";

    public async Task<List<BusinessPartner>> GetAllAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.BusinessPartners.AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<BusinessPartner?> FindByIdAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.BusinessPartners.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    }

    /// <summary>Total outstanding balance across all active partners.</summary>
    public async Task<decimal> TotalBalanceAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.BusinessPartners.Where(p => p.IsActive).SumAsync(p => (decimal?)p.Balance) ?? 0;
    }

    public async Task<int> SaveAsync(BusinessPartner partner)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var row = partner.Id > 0
            ? await db.BusinessPartners.FirstOrDefaultAsync(p => p.Id == partner.Id)
            : null;

        if (row is null)
        {
            partner.CreatedAt = DateTime.Now;
            db.BusinessPartners.Add(partner);
            await db.SaveChangesAsync();
            return partner.Id;
        }

        row.Name = partner.Name;
        row.NRF = partner.NRF;
        row.NUI = partner.NUI;
        row.Address = partner.Address;
        row.City = partner.City;
        row.Phone = partner.Phone;
        row.Email = partner.Email;
        row.PartnerType = partner.PartnerType;
        row.Balance = partner.Balance;
        row.IsActive = partner.IsActive;
        await db.SaveChangesAsync();
        return row.Id;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.BusinessPartners.FirstOrDefaultAsync(p => p.Id == id);
        if (row is null) return false;
        db.BusinessPartners.Remove(row);
        await db.SaveChangesAsync();
        return true;
    }
}
