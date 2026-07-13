using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models
{
    /// <summary>
    /// Business profile types supported by the universal POS platform.
    /// </summary>
    public enum BusinessProfile
    {
        Retail = 0,
        FoodAndBeverage = 1,
        Services = 2,
        Wholesale = 3,
        Pharmacy = 4,
        Rental = 5,
        Hospitality = 6
    }

    /// <summary>
    /// Stores per-installation business configuration, module toggles, and first-run flag.
    /// Exactly one row exists (Id = 1). Singleton — use AppConfiguration to access it.
    /// </summary>
    [Table("BusinessSettings")]
    public class BusinessSettings
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Active business profile, controls which modules are visible.</summary>
        public BusinessProfile Profile { get; set; } = BusinessProfile.Retail;

        /// <summary>True until the first-run wizard is completed.</summary>
        public bool IsFirstRun { get; set; } = true;

        [StringLength(500)]
        public string? LogoPath { get; set; }

        [StringLength(200)]
        public string? BusinessName { get; set; }

        [StringLength(200)]
        public string? Address { get; set; }

        [StringLength(50)]
        public string? Phone { get; set; }

        [StringLength(100)]
        public string? Email { get; set; }

        [StringLength(50)]
        public string? FiscalNumber { get; set; }

        /// <summary>Numri i TVSH-së — the VAT registration number, printed on the A4 tax invoice.</summary>
        [StringLength(50)]
        public string? VatNumber { get; set; }

        /// <summary>Bank account / IBAN, printed on the A4 invoice so a business buyer can pay by transfer.</summary>
        [StringLength(100)]
        public string? BankAccount { get; set; }

        // ── Local hardware (agent) printer targets ────────────────────────────────
        // Windows printer names the on-PC agent sends jobs to. Null = the agent's own
        // env default (RECEIPT_PRINTER / BARCODE_PRINTER). Lets one shop point its
        // courtesy receipt at any thermal printer and its labels at the HPRT.
        [StringLength(100)]
        public string? ReceiptPrinter { get; set; }

        [StringLength(100)]
        public string? BarcodePrinter { get; set; }

        /// <summary>
        /// Where the A4 paper goes: the invoice and the waybill.
        ///
        /// It has to be its own setting, and the agent has to do the printing, because a WEB PAGE
        /// CANNOT CHOOSE A PRINTER — the browser prints to whatever the user picks in the dialog,
        /// and Chrome's kiosk-printing (which is what removes the dialog) always prints to the
        /// Windows DEFAULT printer. On a till the default is the thermal roll, so an A4 invoice
        /// printed through the browser comes out of the receipt printer as a metre of paper.
        /// Null = the agent's INVOICE_PRINTER, else this PC's default printer.
        /// </summary>
        [StringLength(100)]
        public string? InvoicePrinter { get; set; }

        /// <summary>
        /// The ESC/POS code page the thermal printer is switched to before every receipt:
        /// 1252, 852, 858, 850, 1250, 437, or "ascii" to print e/c instead of ë/ç.
        ///
        /// It is a per-shop setting because a printer's firmware decides which pages it really
        /// honours, and nothing but paper can tell you which one that is. The sample slip at
        /// /pajisjet prints one line per candidate; the shop picks the line that came out right.
        /// </summary>
        [StringLength(10)]
        public string ReceiptCodePage { get; set; } = "1252";

        /// <summary>
        /// The label stock in the barcode printer, in mm. The TSPL document is laid out against
        /// these — labels built for 55×25 and printed on 40×30 stock come out cropped, and a
        /// clipped barcode still looks right while scanning nowhere.
        /// </summary>
        public int LabelWidthMm { get; set; } = 55;

        public int LabelHeightMm { get; set; } = 25;

        // ── What the thermal receipt shows ────────────────────────────────────────
        // A receipt is not one document: a kiosk wants a bare total, a wholesaler wants
        // its fiscal number on every slip, and a shop whose cashiers are family does not
        // want their names on paper a customer walks out with. Each of these is a line
        // the shop may or may not want printed, so each is a switch rather than a fork
        // in the layout code. Defaults reproduce exactly what was printed before.
        //
        // These govern the NON-fiscal courtesy receipt only. The fiscal receipt's content
        // is fixed by the ATK and by the fiscal device — nothing here can change it.
        public bool ReceiptShowBusinessName { get; set; } = true;
        public bool ReceiptShowAddress      { get; set; } = true;
        public bool ReceiptShowPhone        { get; set; } = true;
        public bool ReceiptShowFiscalNumber { get; set; } = true;
        public bool ReceiptShowVatNumber    { get; set; } = true;
        public bool ReceiptShowCashier      { get; set; } = true;
        public bool ReceiptShowVatBreakdown { get; set; } = true;

        /// <summary>Cash tendered and change due, printed under the total. Off = the old layout.</summary>
        public bool ReceiptShowPaidAndChange { get; set; }

        /// <summary>Free line above the items — an opening time, a slogan, a promotion.</summary>
        [StringLength(120)]
        public string? ReceiptHeaderNote { get; set; }

        /// <summary>The thank-you at the foot. Blank prints nothing at all.</summary>
        [StringLength(120)]
        public string? ReceiptFooterText { get; set; } = "Faleminderit për blerjen!";

        // ── What the till may print ───────────────────────────────────────────────
        // Four documents come off one sale, and no two shops want the same set of them:
        // a kiosk prints a fiscal receipt and nothing else, a wholesaler prints an A4
        // invoice and a waybill and never touches the thermal roll. So the SET of
        // documents the cashier can choose from is the admin's decision, and the
        // DEFAULT state of each is theirs too — a cashier should not have to tick the
        // same two boxes on every sale of the day.
        //
        // Available = the option is rendered on the till at all (and reprintable after
        // the sale). Default = it starts ticked on a till that has never been set up.
        // A till that HAS been set up keeps the cashier's own choice; hiding a document
        // here overrides both.
        public bool SellOfferFiscal      { get; set; } = true;
        public bool SellOfferReceipt80   { get; set; } = true;
        public bool SellOfferInvoiceA4   { get; set; } = true;
        public bool SellOfferWaybillA4   { get; set; } = true;

        public bool SellDefaultFiscal    { get; set; } = true;
        public bool SellDefaultReceipt80 { get; set; }
        public bool SellDefaultInvoiceA4 { get; set; }
        public bool SellDefaultWaybillA4 { get; set; }

        // ── Module toggles ────────────────────────────────────────────────────────
        public bool EnableTableManagement  { get; set; }
        public bool EnableAppointments     { get; set; }
        public bool EnableRentals          { get; set; }
        public bool EnableKitchenDisplay   { get; set; }
        public bool EnableLoyalty          { get; set; }
        public bool EnableDelivery         { get; set; }
        public bool EnableProductionBOM    { get; set; }
        public bool EnableMultiCurrency    { get; set; }
        public bool EnableShiftManagement  { get; set; } = true;

        // ── Sprint 5 & 6 module toggles ───────────────────────────────────────────
        public bool EnableVariants       { get; set; }
        public bool EnablePriceRules     { get; set; }
        public bool EnablePurchaseOrders { get; set; }

        // ── Sprint 7 & 8 module toggles ───────────────────────────────────────────
        public bool EnableGiftCards      { get; set; }
        public bool EnableBundles        { get; set; }
        public bool EnableNotifications  { get; set; } = true;
        public bool EnableWeighingScale  { get; set; }
        public bool EnableCustomerDisplay { get; set; }

        // ── Sprint 9 & 10 module toggles ──────────────────────────────────────────
        public bool EnableCloudBackup    { get; set; }
        public bool EnableOfflineQueue   { get; set; } = true;
        public bool EnableRestApi        { get; set; }
        public int  RestApiPort          { get; set; } = 5999;

        [StringLength(128)]
        public string? RestApiKey        { get; set; }

        [StringLength(500)]
        public string? WebhookUrl        { get; set; }

        /// <summary>Hour of day (0–23) when the nightly cloud backup runs.</summary>
        public int BackupScheduleHour    { get; set; } = 2;
    }
}
