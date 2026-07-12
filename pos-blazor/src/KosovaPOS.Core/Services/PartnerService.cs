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

    /// <summary>
    /// The partner's running account — every document that moved their balance, oldest first,
    /// with the balance after each one.
    ///
    /// This is the answer to "why does this partner owe 752.86 €": it is not a number the app
    /// invented, it is 82 purchase invoices minus what has been paid against them.
    /// </summary>
    public async Task<List<PartnerLedgerRow>> GetLedgerAsync(int partnerId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var rows = await db.KartelaSubjektit.AsNoTracking()
            .Where(k => k.SubjektiId == partnerId)
            .OrderBy(k => k.Data).ThenBy(k => k.Id)
            .ToListAsync();

        decimal running = 0;
        return rows.Select(k =>
        {
            running += (decimal)(k.Mbeti ?? 0);
            return new PartnerLedgerRow
            {
                Id = k.Id,
                Date = k.Data,
                DocumentNumber = k.DokumentiNr,
                DocumentId = k.DokumentiId,
                Type = k.Tipi ?? "",
                Description = k.Pershkrimi,
                Due = (decimal)(k.PerPagese ?? 0),
                Paid = (decimal)(k.Pagoi ?? 0),
                Remaining = (decimal)(k.Mbeti ?? 0),
                RunningBalance = running,
            };
        }).ToList();
    }

    /// <summary>The document type written for a payment made from this app, as opposed to a BMD import.</summary>
    public const string PaymentType = "PAGESA";

    /// <summary>
    /// Settles money against a partner: a payment WE made to a supplier, or one a customer made
    /// to us. Either way it is a credit on their card — a row with nothing due, the amount paid,
    /// and a negative remainder — so the balance falls by exactly what was handed over.
    ///
    /// Nothing is edited. The existing invoices keep saying what they always said; the ledger is
    /// append-only, which is the whole reason a balance can be explained months later. To zero a
    /// partner off, pay their outstanding balance: the sum of the column reaches zero because the
    /// new row cancels the old ones, not because anything was overwritten.
    /// </summary>
    public async Task<int> RecordPaymentAsync(int partnerId, decimal amount, DateOnly date, string? note)
    {
        if (amount == 0)
            throw new ArgumentException("Një pagesë prej zero nuk regjistrohet.", nameof(amount));

        await using var db = await _dbFactory.CreateDbContextAsync();

        if (!await db.FurnitoriNew.AnyAsync(f => f.Id == partnerId))
            throw new InvalidOperationException("Partneri nuk u gjet.");

        var row = new KartelaSubjektit
        {
            SubjektiId = partnerId,
            Data = date,
            Tipi = PaymentType,
            Pershkrimi = string.IsNullOrWhiteSpace(note) ? "Pagesë" : note!.Trim(),
            PerPagese = 0,
            Pagoi = (double)amount,
            // The signed remainder is what the balance sums. A payment reduces it.
            Mbeti = (double)(-amount),
        };

        db.KartelaSubjektit.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
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

    /// <summary>One line of a partner's account card, with the balance it left behind.</summary>
    public class PartnerLedgerRow
    {
        public int Id { get; set; }
        public DateOnly? Date { get; set; }
        public int? DocumentNumber { get; set; }
        public string? DocumentId { get; set; }
        public string Type { get; set; } = "";
        public string? Description { get; set; }

        /// <summary>What the document obliged: an invoice we received, or one we issued.</summary>
        public decimal Due { get; set; }

        /// <summary>What was settled against it.</summary>
        public decimal Paid { get; set; }

        /// <summary>Due − Paid. Negative on a payment row.</summary>
        public decimal Remaining { get; set; }

        /// <summary>The partner's balance after this row. The last one is the balance shown on the list.</summary>
        public decimal RunningBalance { get; set; }
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
