using Microsoft.EntityFrameworkCore;
using KosovaPOS.Models;
using KosovaPOS.Models.BMDData;
using KosovaPOS.Models.HR;
using KosovaPOS.Models.ATK;

namespace KosovaPOS.Core.Data;

/// <summary>
/// Web (Blazor Server) EF Core context for KosovaPOS.
/// Reuses the desktop POCO models verbatim, but the database provider
/// (Npgsql/Postgres) and connection string are supplied by DI in the web
/// project — this context carries no SQL Server / Windows-auth baggage.
/// Ported from POS2/Database/POSDbContext.cs.
/// </summary>
public class PosDbContext : DbContext
{
    static PosDbContext()
    {
        // The desktop models use DateTime.Now/Today (Kind=Local) as wall-clock
        // values, not tz-aware instants. Legacy behaviour maps DateTime to
        // 'timestamp without time zone' and accepts Local/Unspecified kinds, so
        // the ported services work unchanged. Set before any Npgsql data source
        // is built (static ctor runs on first type access).
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    public PosDbContext(DbContextOptions<PosDbContext> options) : base(options) { }

    // ── BMDData (legacy accounting) models ──────────────────────────────
    public DbSet<Artikujt> Artikujt => Set<Artikujt>();
    public DbSet<ArkaHyrje> ArkaHyrje => Set<ArkaHyrje>();
    public DbSet<ArkaDalje> ArkaDalje => Set<ArkaDalje>();
    public DbSet<ArkaHyrjeDalje> ArkaHyrjeDalje => Set<ArkaHyrjeDalje>();
    public DbSet<Borxhi> Borxhi => Set<Borxhi>();
    public DbSet<DitariH> DitariH => Set<DitariH>();
    public DbSet<DitariD> DitariD => Set<DitariD>();
    public DbSet<FurnitoriNew> FurnitoriNew => Set<FurnitoriNew>();
    public DbSet<Punetoret> Punetoret => Set<Punetoret>();
    public DbSet<Kategoria> Kategoria => Set<Kategoria>();
    public DbSet<KategoriaPos> KategoriaPos => Set<KategoriaPos>();
    public DbSet<Filiala> Filiala => Set<Filiala>();
    public DbSet<Kompania> Kompania => Set<Kompania>();
    public DbSet<Sektori> Sektori => Set<Sektori>();
    public DbSet<Arkat> Arkat => Set<Arkat>();
    public DbSet<MetodaPagese> MetodaPagese => Set<MetodaPagese>();
    public DbSet<Tatimi> Tatimi => Set<Tatimi>();
    public DbSet<Qytetet> Qytetet => Set<Qytetet>();
    public DbSet<NjesitMatese> NjesitMatese => Set<NjesitMatese>();
    public DbSet<LlojiShpenzimeve> LlojiShpenzimeve => Set<LlojiShpenzimeve>();
    public DbSet<POSUser> POSUsers => Set<POSUser>();

    // ── Core POS models ─────────────────────────────────────────────────
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptItem> ReceiptItems => Set<ReceiptItem>();
    public DbSet<BusinessPartner> BusinessPartners => Set<BusinessPartner>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ZReport> ZReports => Set<ZReport>();
    public DbSet<SalesBookEntry> SalesBookEntries => Set<SalesBookEntry>();

    // ── HR module ───────────────────────────────────────────────────────
    public DbSet<Employee> HR_Employees => Set<Employee>();
    public DbSet<EmploymentContract> HR_EmploymentContracts => Set<EmploymentContract>();
    public DbSet<LeaveRequest> HR_LeaveRequests => Set<LeaveRequest>();
    public DbSet<PayrollRecord> HR_PayrollRecords => Set<PayrollRecord>();
    public DbSet<EmployeeCertificate> HR_EmployeeCertificates => Set<EmployeeCertificate>();

    // ── ATK declarations ────────────────────────────────────────────────
    public DbSet<TVShDeclaration> ATK_TVShDeclarations => Set<TVShDeclaration>();
    public DbSet<PayrollTaxDeclaration> ATK_PayrollTaxDeclarations => Set<PayrollTaxDeclaration>();
    public DbSet<CorporateTaxDeclaration> ATK_CorporateTaxDeclarations => Set<CorporateTaxDeclaration>();
    public DbSet<WithholdingTaxDeclaration> ATK_WithholdingTaxDeclarations => Set<WithholdingTaxDeclaration>();

    // ── Universal POS platform ──────────────────────────────────────────
    public DbSet<BusinessSettings> BusinessSettings => Set<BusinessSettings>();
    public DbSet<CashShift> CashShifts => Set<CashShift>();

    // ── Returns & refunds ───────────────────────────────────────────────
    public DbSet<ReturnReceipt> ReturnReceipts => Set<ReturnReceipt>();
    public DbSet<ReturnReceiptItem> ReturnReceiptItems => Set<ReturnReceiptItem>();

    // ── Table management & kitchen display ──────────────────────────────
    public DbSet<RestaurantTable> RestaurantTables => Set<RestaurantTable>();
    public DbSet<TableSection> TableSections => Set<TableSection>();
    public DbSet<KitchenOrder> KitchenOrders => Set<KitchenOrder>();

    // ── Appointments & loyalty ──────────────────────────────────────────
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<EmployeeSchedule> EmployeeSchedules => Set<EmployeeSchedule>();
    public DbSet<LoyaltyAccount> LoyaltyAccounts => Set<LoyaltyAccount>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    public DbSet<LoyaltyConfig> LoyaltyConfigs => Set<LoyaltyConfig>();

    // ── Product variants & price rules ──────────────────────────────────
    public DbSet<ArticleVariant> ArticleVariants => Set<ArticleVariant>();
    public DbSet<PriceRule> PriceRules => Set<PriceRule>();

    // ── Rental management & supplier purchase orders ────────────────────
    public DbSet<RentalItem> RentalItems => Set<RentalItem>();
    public DbSet<RentalAgreement> RentalAgreements => Set<RentalAgreement>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();

    // ── Delivery orders & gift cards ────────────────────────────────────
    public DbSet<DeliveryOrder> DeliveryOrders => Set<DeliveryOrder>();
    public DbSet<GiftCard> GiftCards => Set<GiftCard>();
    public DbSet<GiftCardTransaction> GiftCardTransactions => Set<GiftCardTransaction>();

    // ── Product bundles & notifications ─────────────────────────────────
    public DbSet<ProductBundle> ProductBundles => Set<ProductBundle>();
    public DbSet<AlertConfig> AlertConfigs => Set<AlertConfig>();
    public DbSet<NotificationItem> NotificationItems => Set<NotificationItem>();

    // ── Multi-currency, offline queue, REST API log ─────────────────────
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<OfflineQueueLog> OfflineQueueLogs => Set<OfflineQueueLog>();
    public DbSet<ApiRequestLog> ApiRequestLogs => Set<ApiRequestLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Table name mappings ported verbatim from the desktop context so the
        // generated Postgres schema keeps the same table names.
        modelBuilder.Entity<Artikujt>().ToTable("Artikujt");
        modelBuilder.Entity<ArkaHyrje>().ToTable("ArkaHyrje");
        modelBuilder.Entity<ArkaDalje>().ToTable("ArkaDalje");
        modelBuilder.Entity<ArkaHyrjeDalje>().ToTable("ArkaHyrjeDalje");
        modelBuilder.Entity<Borxhi>().ToTable("Borxhi");
        modelBuilder.Entity<DitariH>().ToTable("DitariH");
        modelBuilder.Entity<DitariD>().ToTable("DitariD");
        modelBuilder.Entity<FurnitoriNew>().ToTable("FurnitoriNew");
        modelBuilder.Entity<Punetoret>().ToTable("Punetoret");
        modelBuilder.Entity<Kategoria>().ToTable("Kategoria");
        modelBuilder.Entity<KategoriaPos>().ToTable("KategoriaPos");
        modelBuilder.Entity<Filiala>().ToTable("Filiala");
        modelBuilder.Entity<Kompania>().ToTable("Kompania");
        modelBuilder.Entity<Sektori>().ToTable("Sektori");
        modelBuilder.Entity<Arkat>().ToTable("Arkat");
        modelBuilder.Entity<MetodaPagese>().ToTable("MetodaPagese");
        modelBuilder.Entity<Tatimi>().ToTable("Tatimi");
        modelBuilder.Entity<Qytetet>().ToTable("Qytetet");
        modelBuilder.Entity<NjesitMatese>().ToTable("NjesitMatese");
        modelBuilder.Entity<LlojiShpenzimeve>().ToTable("LlojiShpenzimeve");
        modelBuilder.Entity<POSUser>().ToTable("POSUsers");

        modelBuilder.Entity<Article>().ToTable("Articles");
        modelBuilder.Entity<Receipt>().ToTable("Receipts");
        modelBuilder.Entity<ReceiptItem>().ToTable("ReceiptItems");
        modelBuilder.Entity<BusinessPartner>().ToTable("BusinessPartners");
        modelBuilder.Entity<Purchase>().ToTable("Purchases");
        modelBuilder.Entity<PurchaseItem>().ToTable("PurchaseItems");
        modelBuilder.Entity<User>().ToTable("POSUsers_Legacy");
        modelBuilder.Entity<AuditLog>().ToTable("AuditLogs");
        modelBuilder.Entity<ZReport>().ToTable("ZReports");
        modelBuilder.Entity<SalesBookEntry>().ToTable("SalesBookEntries");

        modelBuilder.Entity<BusinessSettings>().ToTable("BusinessSettings");
        modelBuilder.Entity<CashShift>().ToTable("CashShifts");

        modelBuilder.Entity<ArticleVariant>().ToTable("ArticleVariants");
        modelBuilder.Entity<PriceRule>().ToTable("PriceRules");

        modelBuilder.Entity<RentalItem>().ToTable("RentalItems");
        modelBuilder.Entity<RentalAgreement>().ToTable("RentalAgreements");
        modelBuilder.Entity<PurchaseOrder>().ToTable("PurchaseOrders");
        modelBuilder.Entity<PurchaseOrderItem>().ToTable("PurchaseOrderItems");
    }
}
