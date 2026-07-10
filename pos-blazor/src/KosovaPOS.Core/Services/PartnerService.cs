using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Models;
using KosovaPOS.Models.BMDData;

namespace KosovaPOS.Core.Services;

/// <summary>
/// CRUD over the shop's business partners — customers, suppliers, or both.
///
/// These live in the BMD table <see cref="FurnitoriNew"/> (F = is-supplier,
/// K = is-customer), which is what the desktop wrote and what PurchaseService
/// already reads. The <c>BusinessPartners</c> table is empty and always was;
/// this service used to query it, which is why /partneret rendered nothing while
/// the purchase screen's supplier dropdown worked.
///
/// <see cref="BusinessPartner"/> is kept as the page's view model. Two of its
/// properties have no column behind them:
///   - <c>Balance</c> is derived from <see cref="KartelaSubjektit"/> (sum of Mbeti)
///     and is therefore read-only;
///   - <c>IsActive</c> has no equivalent in BMD, so every partner reads as active.
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

        // Outstanding balance per partner, summed over their ledger rows.
        var balances = await db.KartelaSubjektit.AsNoTracking()
            .Where(k => k.SubjektiId != null)
            .GroupBy(k => k.SubjektiId!.Value)
            .Select(g => new { SubjektiId = g.Key, Total = g.Sum(k => k.Mbeti ?? 0) })
            .ToDictionaryAsync(x => x.SubjektiId, x => x.Total);

        var cities = await db.Qytetet.AsNoTracking().ToDictionaryAsync(c => c.ID, c => c.Emertimi);

        var rows = await db.FurnitoriNew.AsNoTracking().OrderBy(f => f.Emri).ToListAsync();

        return rows.Select(f => ToPartner(f, cities, balances)).ToList();
    }

    public async Task<BusinessPartner?> FindByIdAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.FurnitoriNew.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
        if (row is null) return null;

        var cities = await db.Qytetet.AsNoTracking().ToDictionaryAsync(c => c.ID, c => c.Emertimi);
        var balance = await db.KartelaSubjektit.AsNoTracking()
            .Where(k => k.SubjektiId == id)
            .SumAsync(k => (double?)k.Mbeti) ?? 0;

        return ToPartner(row, cities, new Dictionary<int, double> { [id] = balance });
    }

    /// <summary>Total outstanding across every partner ledger.</summary>
    public async Task<decimal> TotalBalanceAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var total = await db.KartelaSubjektit.AsNoTracking().SumAsync(k => (double?)k.Mbeti) ?? 0;
        return (decimal)total;
    }

    /// <summary>Cities for the editor dropdown, so Qyteti keeps a real FK value.</summary>
    public async Task<List<Qytetet>> GetCitiesAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Qytetet.AsNoTracking().OrderBy(c => c.Emertimi).ToListAsync();
    }

    /// <summary>
    /// Inserts or updates the partner. <c>Balance</c> is ignored: it is a projection
    /// of the ledger, not a stored field, and writing it here would silently diverge
    /// from Kartela_Subjektit.
    /// </summary>
    public async Task<int> SaveAsync(BusinessPartner partner)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var cityId = await ResolveCityIdAsync(db, partner.City);

        var row = partner.Id > 0
            ? await db.FurnitoriNew.FirstOrDefaultAsync(f => f.Id == partner.Id)
            : null;

        if (row is null)
        {
            row = new FurnitoriNew { Data = DateTime.Now.ToString("dd.MM.yyyy") };
            db.FurnitoriNew.Add(row);
        }

        row.Emri = partner.Name;
        row.NRF = partner.NRF;
        row.NIT = partner.NUI;
        row.Adresa = partner.Address;
        row.Qyteti = cityId;
        row.Telefoni = partner.Phone;
        row.Email = partner.Email;
        row.F = partner.PartnerType is Supplier or Both;
        row.K = partner.PartnerType is Customer or Both;

        await db.SaveChangesAsync();
        return (int)row.Id;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.FurnitoriNew.FirstOrDefaultAsync(f => f.Id == id);
        if (row is null) return false;
        db.FurnitoriNew.Remove(row);
        await db.SaveChangesAsync();
        return true;
    }

    private static async Task<int?> ResolveCityIdAsync(PosDbContext db, string? city)
    {
        if (string.IsNullOrWhiteSpace(city)) return null;
        var name = city.Trim();
        var match = await db.Qytetet.FirstOrDefaultAsync(c => c.Emertimi == name);
        return match?.ID;
    }

    private static BusinessPartner ToPartner(
        FurnitoriNew f,
        IReadOnlyDictionary<int, string?> cities,
        IReadOnlyDictionary<int, double> balances)
    {
        var city = f.Qyteti is int q && cities.TryGetValue(q, out var name) ? name : null;
        var balance = balances.TryGetValue((int)f.Id, out var b) ? (decimal)b : 0m;

        return new BusinessPartner
        {
            Id = (int)f.Id,
            Name = f.Emri ?? string.Empty,
            NRF = f.NRF,
            NUI = f.NIT,
            Address = f.Adresa,
            City = city,
            Phone = f.Telefoni,
            Email = f.Email,
            PartnerType = f.PartnerType,
            Balance = balance,
            IsActive = true,
        };
    }
}
