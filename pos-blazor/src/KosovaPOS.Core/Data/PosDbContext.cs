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
        // Belt and braces: every entry point already calls this before opening any
        // context (see NpgsqlCompat). Relying on this static constructor alone is
        // what broke ControlDbContext, which gets used first at startup.
        NpgsqlCompat.EnableLegacyTimestampBehavior();
    }

    public PosDbContext(DbContextOptions<PosDbContext> options) : base(options) { }

    /// <summary>
    /// The business database this context is connected to — the key everything cached in
    /// memory is filed under. Read from the connection string, so it costs nothing and needs
    /// no open connection.
    /// </summary>
    public string DatabaseName => Database.GetDbConnection().Database;

    /// <summary>
    /// Saves, then tells the caches what changed. The stamps are bumped from the change
    /// tracker rather than by the calling service, so a new write path cannot forget to
    /// invalidate — the reason cached stock quantities would otherwise start lying to a cashier.
    ///
    /// Bumped only after the save succeeds: a rolled-back transaction changed nothing, and
    /// throwing away a good cache for it would just cost the next reader a query.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var catalogTouched = ChangeTracker.Entries<Artikujt>().Any(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
        var settingsTouched = ChangeTracker.Entries<BusinessSettings>().Any(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

        var written = await base.SaveChangesAsync(cancellationToken);

        if (catalogTouched) DataVersions.Bump(DatabaseName, DataVersions.Catalog);
        if (settingsTouched) DataVersions.Bump(DatabaseName, DataVersions.Settings);

        return written;
    }

    /// <summary>Nothing in this codebase saves synchronously, but a cache that only invalidates
    /// on one of the two save paths is a trap laid for whoever writes the first one.</summary>
    public override int SaveChanges()
    {
        var catalogTouched = ChangeTracker.Entries<Artikujt>().Any(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
        var settingsTouched = ChangeTracker.Entries<BusinessSettings>().Any(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

        var written = base.SaveChanges();

        if (catalogTouched) DataVersions.Bump(DatabaseName, DataVersions.Catalog);
        if (settingsTouched) DataVersions.Bump(DatabaseName, DataVersions.Settings);

        return written;
    }

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
    public DbSet<TblStoku> TblStoku => Set<TblStoku>();
    public DbSet<KartelaSubjektit> KartelaSubjektit => Set<KartelaSubjektit>();

    // ── Core POS models ─────────────────────────────────────────────────
    public DbSet<Article> Articles => Set<Article>();
    /// <summary>
    /// ⚠️ Empty, and nothing writes it. A completed sale is persisted as journal rows
    /// in <see cref="DitariD"/> (see SalesService.SaveReceiptAsync) — <see cref="Receipt"/>
    /// survives only as the in-memory object the Sale screen builds and the receipt/invoice
    /// views render. Do not add columns here expecting a sale to fill them in: it won't.
    /// </summary>
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptItem> ReceiptItems => Set<ReceiptItem>();
    /// <summary>
    /// Empty, and always has been — BMDData never populated it. The shop's real
    /// customers and suppliers live in <see cref="FurnitoriNew"/>; read them from
    /// there (see PartnerService). <see cref="BusinessPartner"/> survives only as
    /// the view model that <c>/partneret</c> binds to.
    /// </summary>
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
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

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

        modelBuilder.Entity<TblStoku>(e =>
        {
            e.ToTable("tbl_Stoku");
            e.HasIndex(s => s.ArtikulliId);
            e.HasIndex(s => s.Data);
        });
        modelBuilder.Entity<KartelaSubjektit>(e =>
        {
            e.ToTable("Kartela_Subjektit");
            e.HasIndex(k => k.SubjektiId);
            e.HasIndex(k => k.Data);
        });

        modelBuilder.Entity<Article>().ToTable("Articles");
        modelBuilder.Entity<Receipt>().ToTable("Receipts");
        modelBuilder.Entity<ReceiptItem>().ToTable("ReceiptItems");
        modelBuilder.Entity<BusinessPartner>().ToTable("BusinessPartners");
        modelBuilder.Entity<Purchase>().ToTable("Purchases");
        modelBuilder.Entity<PurchaseItem>().ToTable("PurchaseItems");
        modelBuilder.Entity<User>().ToTable("POSUsers_Legacy");
        modelBuilder.Entity<AuditLog>().ToTable("AuditLogs");
        // /auditimi reads newest-first and filters by day; an append-only table read
        // in reverse is the one case where the index is not optional.
        modelBuilder.Entity<AuditLog>().HasIndex(x => x.Timestamp);
        modelBuilder.Entity<ZReport>().ToTable("ZReports");
        modelBuilder.Entity<SalesBookEntry>().ToTable("SalesBookEntries");

        modelBuilder.Entity<BusinessSettings>().ToTable("BusinessSettings");
        modelBuilder.Entity<CashShift>().ToTable("CashShifts");

        modelBuilder.Entity<StockMovement>(e =>
        {
            e.ToTable("StockMovements");
            e.HasIndex(m => m.MovedAt);
            e.HasIndex(m => m.ArticleId);
            e.Property(m => m.Quantity).HasPrecision(18, 3);
            e.Property(m => m.QuantityBefore).HasPrecision(18, 3);
            e.Property(m => m.QuantityAfter).HasPrecision(18, 3);
            e.Property(m => m.UnitCost).HasPrecision(18, 4);
        });

        modelBuilder.Entity<ArticleVariant>().ToTable("ArticleVariants");
        modelBuilder.Entity<PriceRule>().ToTable("PriceRules");

        modelBuilder.Entity<RentalItem>().ToTable("RentalItems");
        modelBuilder.Entity<RentalAgreement>().ToTable("RentalAgreements");
        modelBuilder.Entity<PurchaseOrder>().ToTable("PurchaseOrders");
        modelBuilder.Entity<PurchaseOrderItem>().ToTable("PurchaseOrderItems");
    }
}
