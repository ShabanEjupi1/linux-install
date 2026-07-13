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
/// properties have no meaning here:
///   - <c>IsActive</c> has no equivalent in BMD, so every partner reads as active;
///   - <c>Balance</c> is a leftover column on the empty BusinessPartners table. It is
///     never read and never written. Do not resurrect it — see below.
///
/// ── Why this service does not compute a balance ──────────────────────────────────
///
/// It used to: balance = SUM(Kartela_Subjektit.Mbeti) per partner. That sum is not a
/// balance, and the shop was shown €3,835 of supplier credit that did not exist.
///
/// Mbeti is a per-row remainder BMD writes against a single document, and BMD records
/// a bulk payment by stamping the WHOLE payment onto whichever document row it happens
/// to be editing. Motto Com's card carries a €199.26 purchase (01-BV310) with €2,515.03
/// "paid" against it — that €2,515.03 settled many invoices, and each of those invoices
/// already shows itself settled (its own row has Pagoi == PerPagese, remainder 0). So
/// every euro the shop actually pays is subtracted twice, and a supplier drifts negative
/// by roughly what they were paid. Across the table: purchases +3,637, payments and edit
/// rows −7,473, and not one partner in debt — which is the tell, because a real supplier
/// ledger always has something open.
///
/// The column cannot be summed, and no arithmetic here can recover what it should have
/// been: the information needed to match a bulk payment to the invoices it cleared was
/// never imported. Writing offsetting rows to force the total to zero was considered and
/// rejected — it fabricates accounting entries to hide a bad import, and the next BMD
/// import re-creates the drift anyway.
///
/// So the card shows what BMD wrote, document by document, and the app draws no conclusion
/// from it. Money that moves through THIS app is recorded by <see cref="RecordPaymentAsync"/>
/// and is trustworthy; the imported history is evidence, not arithmetic.
/// Ported from POS2/Windows/BusinessPartnersWindow + BusinessPartnerEditWindow.
/// </summary>
public class PartnerService
{
    private readonly IDbContextFactory<PosDbContext> _dbFactory;

    public PartnerService(IDbContextFactory<PosDbContext> dbFactory) => _dbFactory = dbFactory;

    public const string Customer = "Klient";
    public const string Supplier = "Furnizues";
    public const string Both = "Të dy";

    /// <summary>
    /// Money, from a column that is not money.
    ///
    /// The BMD amounts are <see cref="double"/>, so a figure that should be exactly zero
    /// arrives as −1.42e−14 and, cast straight to decimal, formats as "0.00 €" while still
    /// comparing as non-zero — which is how a settled document used to get painted red.
    /// Rounding to the cent at the boundary is the fix: below half a cent there is no such
    /// thing as a debt, because there is no coin that could settle it.
    /// </summary>
    public static decimal Money(double? raw) =>
        Math.Round((decimal)(raw ?? 0), 2, MidpointRounding.AwayFromZero);

    public async Task<List<BusinessPartner>> GetAllAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var cities = await db.Qytetet.AsNoTracking().ToDictionaryAsync(c => c.ID, c => c.Emertimi);

        var rows = await db.FurnitoriNew.AsNoTracking().OrderBy(f => f.Emri).ToListAsync();

        return rows.Select(f => ToPartner(f, cities)).ToList();
    }

    public async Task<BusinessPartner?> FindByIdAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var row = await db.FurnitoriNew.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
        if (row is null) return null;

        var cities = await db.Qytetet.AsNoTracking().ToDictionaryAsync(c => c.ID, c => c.Emertimi);
        return ToPartner(row, cities);
    }

    /// <summary>
    /// The partner's account card: every document BMD holds against them, oldest first,
    /// exactly as BMD wrote it.
    ///
    /// There is deliberately no running balance. See the class remarks — the remainders in
    /// this table do not add up to one, and a column of numbers that invites you to add them
    /// is how the shop came to believe it was €3,835 in credit with its suppliers. Each row
    /// is a fact about one document; the total is not a fact about anything.
    /// </summary>
    public async Task<List<PartnerLedgerRow>> GetLedgerAsync(int partnerId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var rows = await db.KartelaSubjektit.AsNoTracking()
            .Where(k => k.SubjektiId == partnerId)
            .OrderBy(k => k.Data).ThenBy(k => k.Id)
            .ToListAsync();

        return rows.Select(k => new PartnerLedgerRow
        {
            Id = k.Id,
            Date = k.Data,
            DocumentNumber = k.DokumentiNr,
            DocumentId = k.DokumentiId,
            Type = k.Tipi ?? "",
            Description = k.Pershkrimi,
            Due = Money(k.PerPagese),
            Paid = Money(k.Pagoi),
            Remaining = Money(k.Mbeti),
        }).ToList();
    }

    /// <summary>The document type written for a payment made from this app, as opposed to a BMD import.</summary>
    public const string PaymentType = "PAGESA";

    /// <summary>
    /// Settles money against a partner: a payment WE made to a supplier, or one a customer made
    /// to us. Either way it lands on their card as a row with nothing due, the amount paid, and
    /// a negative remainder.
    ///
    /// Nothing is edited. The existing documents keep saying what they always said; the ledger is
    /// append-only, which is the whole reason a figure can still be explained months later. This
    /// row is a record that money changed hands on a date — it is not an instruction to the app
    /// to revise anything, and no total is derived from it (see the class remarks).
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
    /// Inserts or updates the partner. <c>Balance</c> is ignored — the app holds no
    /// opinion about what a partner's balance is, and a field that let someone type one
    /// in would be inventing the very number this service refuses to invent.
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

        /// <summary>
        /// Due − Paid, for THIS document, as BMD left it. Negative on a payment row.
        /// Do not sum this column across rows — see the class remarks.
        /// </summary>
        public decimal Remaining { get; set; }
    }

    private static BusinessPartner ToPartner(
        FurnitoriNew f,
        IReadOnlyDictionary<int, string?> cities)
    {
        var city = f.Qyteti is int q && cities.TryGetValue(q, out var name) ? name : null;

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
            IsActive = true,
        };
    }
}
