using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Core.Printing;
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

        row.BusinessName   = Clean(input.BusinessName);
        row.Address        = Clean(input.Address);
        row.FiscalNumber   = Clean(input.FiscalNumber);
        row.VatNumber      = Clean(input.VatNumber);
        row.BankAccount    = Clean(input.BankAccount);
        row.ReceiptPrinter = Clean(input.ReceiptPrinter);
        row.BarcodePrinter = Clean(input.BarcodePrinter);
        row.InvoicePrinter = Clean(input.InvoicePrinter);
        row.Profile        = input.Profile;
        row.IsFirstRun     = false;

        // This method copies a whitelist, not the object: a field missing from the list below is
        // silently dropped, and the screen that set it still reports "U ruajt". Anything added to
        // BusinessSettings has to be added here too.
        row.ReceiptCodePage = string.IsNullOrWhiteSpace(input.ReceiptCodePage)
            ? ReceiptCodePages.Default
            : input.ReceiptCodePage.Trim();
        // Clamped to the range the TSPL layout is willing to lay out; a 0 here (a settings row
        // written before these columns existed) means "not set", not "a label 0mm wide".
        row.LabelWidthMm  = input.LabelWidthMm  is > 0 ? Math.Clamp(input.LabelWidthMm, 20, 200) : 55;
        row.LabelHeightMm = input.LabelHeightMm is > 0 ? Math.Clamp(input.LabelHeightMm, 10, 200) : 25;

        // What the courtesy receipt prints. (See the whitelist warning above — leaving these out
        // is exactly the failure it describes: the toggles moved, the preview followed them, the
        // screen said "U ruajt", and the printer went on printing the old receipt.)
        row.ReceiptShowBusinessName  = input.ReceiptShowBusinessName;
        row.ReceiptShowAddress       = input.ReceiptShowAddress;
        row.ReceiptShowPhone         = input.ReceiptShowPhone;
        row.ReceiptShowFiscalNumber  = input.ReceiptShowFiscalNumber;
        row.ReceiptShowVatNumber     = input.ReceiptShowVatNumber;
        row.ReceiptShowCashier       = input.ReceiptShowCashier;
        row.ReceiptShowVatBreakdown  = input.ReceiptShowVatBreakdown;
        row.ReceiptShowPaidAndChange = input.ReceiptShowPaidAndChange;
        row.ReceiptHeaderNote        = Clean(input.ReceiptHeaderNote);
        // Blank means "print no footer at all" — a real choice, so it is stored as null and not
        // quietly replaced with the default thank-you.
        row.ReceiptFooterText        = Clean(input.ReceiptFooterText);

        // Which documents the till offers, and which of them start ticked.
        row.SellOfferFiscal      = input.SellOfferFiscal;
        row.SellOfferReceipt80   = input.SellOfferReceipt80;
        row.SellOfferInvoiceA4   = input.SellOfferInvoiceA4;
        row.SellOfferWaybillA4   = input.SellOfferWaybillA4;
        // A document that is not offered cannot be a default — otherwise a till set up
        // afresh would silently print paper the admin has taken off the screen.
        row.SellDefaultFiscal    = input.SellDefaultFiscal    && input.SellOfferFiscal;
        row.SellDefaultReceipt80 = input.SellDefaultReceipt80 && input.SellOfferReceipt80;
        row.SellDefaultInvoiceA4 = input.SellDefaultInvoiceA4 && input.SellOfferInvoiceA4;
        row.SellDefaultWaybillA4 = input.SellDefaultWaybillA4 && input.SellOfferWaybillA4;

        await db.SaveChangesAsync();

        static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
