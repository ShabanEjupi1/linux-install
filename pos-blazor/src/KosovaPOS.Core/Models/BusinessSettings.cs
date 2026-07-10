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
