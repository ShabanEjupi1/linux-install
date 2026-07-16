using Microsoft.EntityFrameworkCore;
using KosovaPOS.Models;
using KosovaPOS.Models.BMDData;
using System;
using Microsoft.Data.SqlClient;

namespace KosovaPOS.Database
{
    /// <summary>
    /// Database context for KosovaPOS - SQL Server only (BMDData database)
    /// </summary>
    public class POSDbContext : DbContext
    {
        private static bool _connectionValidated = false;
        private static string? _lastConnectionError;

        /// <summary>
        /// SQL Server is always used - no fallback
        /// </summary>
        public static bool UseSqlServer { get; set; } = true;
        
        /// <summary>
        /// Get the last connection error if any
        /// </summary>
        public static string? LastConnectionError => _lastConnectionError;
        
        /// <summary>
        /// Validate SQL Server connection and return true if successful
        /// </summary>
        public static bool ValidateConnection()
        {
            if (_connectionValidated) return true;

            var server = Environment.GetEnvironmentVariable("SQL_SERVER") ?? "(localdb)\\MSSQLLocalDB";
            var database = Environment.GetEnvironmentVariable("SQL_DATABASE") ?? "BMDData";

            // Try the configured server first
            if (TryConnect(server, database, out var error))
            {
                _connectionValidated = true;
                _lastConnectionError = null;
                return true;
            }

            // If configured server fails, try common alternatives
            var alternativeServers = new[] 
            {
                "(localdb)\\MSSQLLocalDB",
                "localhost",
                ".\\SQLEXPRESS",
                "localhost\\SQLEXPRESS",
                "DESKTOP-25RVD2U"
            };

            foreach (var altServer in alternativeServers)
            {
                if (altServer == server) continue; // Already tried

                if (TryConnect(altServer, database, out _))
                {
                    _connectionValidated = true;
                    _lastConnectionError = $"Connected using '{altServer}' instead of configured '{server}'. Consider updating SQL_SERVER environment variable.";
                    return true;
                }
            }

            // All attempts failed
            _lastConnectionError = BuildDetailedErrorMessage(server, database, error);
            _connectionValidated = false;
            return false;
        }

        /// <summary>
        /// Try to connect to a specific SQL Server instance
        /// </summary>
        private static bool TryConnect(string server, string database, out string errorMessage)
        {
            try
            {
                var connectionString = $"Server={server};Database={database};Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=5;";

                using var connection = new SqlConnection(connectionString);
                connection.Open();

                using var command = new SqlCommand("SELECT 1", connection);
                command.ExecuteScalar();

                errorMessage = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Build a detailed error message with troubleshooting steps
        /// </summary>
        private static string BuildDetailedErrorMessage(string server, string database, string originalError)
        {
            var message = $"Failed to connect to SQL Server '{server}', database '{database}'.\n\n";
            message += $"Original Error: {originalError}\n\n";
            message += "Troubleshooting steps:\n";
            message += "1. Check if SQL Server is running:\n";
            message += "   - Open 'Services' (services.msc)\n";
            message += "   - Look for 'SQL Server (MSSQLSERVER)' or 'SQL Server (SQLEXPRESS)'\n";
            message += "   - Ensure the service is running\n\n";
            message += "2. Verify the server name:\n";
            message += "   - Common names: (localdb)\\MSSQLLocalDB, localhost, .\\SQLEXPRESS\n";
            message += "   - Update SQL_SERVER in your .env file\n\n";
            message += "3. Check SQL Server configuration:\n";
            message += "   - Open SQL Server Configuration Manager\n";
            message += "   - Enable TCP/IP and Named Pipes protocols\n";
            message += "   - Restart SQL Server service\n\n";
            message += "4. Verify Windows Authentication is enabled\n";
            message += "5. Check if the database exists (create it if needed)";

            return message;
        }
        
        /// <summary>
        /// Reset connection validation to force re-check
        /// </summary>
        public static void ResetConnectionValidation()
        {
            _connectionValidated = false;
            _lastConnectionError = null;
        }
        
        // SQL Server BMDData models
        public DbSet<Artikujt> Artikujt { get; set; }
        public DbSet<ArkaHyrje> ArkaHyrje { get; set; }
        public DbSet<ArkaDalje> ArkaDalje { get; set; }
        public DbSet<ArkaHyrjeDalje> ArkaHyrjeDalje { get; set; }
        public DbSet<Borxhi> Borxhi { get; set; }
        public DbSet<DitariH> DitariH { get; set; }
        public DbSet<DitariD> DitariD { get; set; }
        public DbSet<FurnitoriNew> FurnitoriNew { get; set; }
        public DbSet<Punetoret> Punetoret { get; set; }
        public DbSet<Kategoria> Kategoria { get; set; }
        public DbSet<KategoriaPos> KategoriaPos { get; set; }
        public DbSet<Filiala> Filiala { get; set; }
        public DbSet<Kompania> Kompania { get; set; }
        public DbSet<Sektori> Sektori { get; set; }
        public DbSet<Arkat> Arkat { get; set; }
        public DbSet<MetodaPagese> MetodaPagese { get; set; }
        public DbSet<Tatimi> Tatimi { get; set; }
        public DbSet<Qytetet> Qytetet { get; set; }
        public DbSet<NjesitMatese> NjesitMatese { get; set; }
        public DbSet<LlojiShpenzimeve> LlojiShpenzimeve { get; set; }
        public DbSet<POSUser> POSUsers { get; set; }
        
        // Legacy DbSets - retained for backward compatibility with utility/test tools
        // These map to SQL Server tables and can be removed when utility tools are updated
        public DbSet<Article> Articles { get; set; }
        public DbSet<Receipt> Receipts { get; set; }
        public DbSet<ReceiptItem> ReceiptItems { get; set; }
        public DbSet<BusinessPartner> BusinessPartners { get; set; }
        public DbSet<Purchase> Purchases { get; set; }
        public DbSet<PurchaseItem> PurchaseItems { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        
        // Pizzeria-specific models
        public DbSet<PizzaTopping> PizzaToppings { get; set; }
        public DbSet<PizzaOrderTopping> PizzaOrderToppings { get; set; }
        public DbSet<PizzaCategory> PizzaCategories { get; set; }

        // Restaurant management models
        public DbSet<RestaurantTable> RestaurantTables { get; set; }
        public DbSet<TableReservation> TableReservations { get; set; }
        public DbSet<DeliveryOrder> DeliveryOrders { get; set; }
        public DbSet<DeliveryDriver> DeliveryDrivers { get; set; }
        public DbSet<OnlineOrder> OnlineOrders { get; set; }

        // Inventory management models
        public DbSet<InventoryStock> InventoryStocks { get; set; }
        public DbSet<InventoryMovement> InventoryMovements { get; set; }
        public DbSet<InventorySupplier> InventorySuppliers { get; set; }
        public DbSet<ArticleSupplier> ArticleSuppliers { get; set; }
        public DbSet<ReorderSuggestion> ReorderSuggestions { get; set; }
        public DbSet<StockAlert> StockAlerts { get; set; }

        // Customer loyalty models
        public DbSet<Customer> Customers { get; set; }
        public DbSet<LoyaltyTransaction> LoyaltyTransactions { get; set; }
        public DbSet<Campaign> Campaigns { get; set; }
        public DbSet<CampaignUsage> CampaignUsages { get; set; }
        public DbSet<CustomerOrderHistory> CustomerOrderHistories { get; set; }

        // Staff scheduling models
        public DbSet<StaffMember> StaffMembers { get; set; }
        public DbSet<WorkShift> WorkShifts { get; set; }
        public DbSet<EmployeePerformance> EmployeePerformances { get; set; }
        public DbSet<TimeCard> TimeCards { get; set; }
        public DbSet<LaborCostAnalysis> LaborCostAnalyses { get; set; }
        public DbSet<EmployeeAvailability> EmployeeAvailabilities { get; set; }
        public DbSet<TimeOffRequest> TimeOffRequests { get; set; }

        // Kitchen display system models
        public DbSet<KitchenOrder> KitchenOrders { get; set; }
        public DbSet<KitchenOrderItem> KitchenOrderItems { get; set; }
        public DbSet<IngredientPrep> IngredientPreps { get; set; }
        public DbSet<KitchenStation> KitchenStations { get; set; }
        public DbSet<KitchenPerformance> KitchenPerformances { get; set; }

        // Parameterless constructor
        public POSDbContext() : base()
        {
        }
        
        // Constructor with options for explicit configuration
        public POSDbContext(DbContextOptions<POSDbContext> options) : base(options)
        {
        }
        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Only configure if not already configured
            if (!optionsBuilder.IsConfigured)
            {
                // SQL Server configuration - BMDData database
                // Default to LocalDB if not specified
                var server = Environment.GetEnvironmentVariable("SQL_SERVER") ?? "(localdb)\\MSSQLLocalDB";
                var database = Environment.GetEnvironmentVariable("SQL_DATABASE") ?? "BMDData";
                var connectionString = $"Server={server};Database={database};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;Connection Timeout=30;";

                optionsBuilder.UseSqlServer(connectionString, options =>
                {
                    options.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                    options.CommandTimeout(60);
                });
            }
        }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // SQL Server BMDData table mappings
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
            
            // Legacy entity mappings - these can be stored as separate tables
            // or linked to existing BMDData tables based on requirements
            modelBuilder.Entity<Article>().ToTable("Articles");
            modelBuilder.Entity<Receipt>().ToTable("Receipts");
            modelBuilder.Entity<ReceiptItem>().ToTable("ReceiptItems");
            modelBuilder.Entity<BusinessPartner>().ToTable("BusinessPartners");
            modelBuilder.Entity<Purchase>().ToTable("Purchases");
            modelBuilder.Entity<PurchaseItem>().ToTable("PurchaseItems");
            modelBuilder.Entity<User>().ToTable("POSUsers_Legacy");
            modelBuilder.Entity<AuditLog>().ToTable("AuditLogs");
            
            // Pizzeria entity mappings
            modelBuilder.Entity<PizzaTopping>().ToTable("PizzaToppings");
            modelBuilder.Entity<PizzaOrderTopping>().ToTable("PizzaOrderToppings");
            modelBuilder.Entity<PizzaCategory>().ToTable("PizzaCategories");

            // Restaurant management entity mappings
            modelBuilder.Entity<RestaurantTable>().ToTable("RestaurantTables");
            modelBuilder.Entity<TableReservation>().ToTable("TableReservations");
            modelBuilder.Entity<DeliveryOrder>().ToTable("DeliveryOrders");
            modelBuilder.Entity<DeliveryOrder>()
                .Property(d => d.EstimatedDeliveryTime)
                .HasColumnName("EstDeliveryMinutes");
            modelBuilder.Entity<DeliveryDriver>().ToTable("DeliveryDrivers");
            modelBuilder.Entity<DeliveryDriver>()
                .Property(d => d.AverageRating)
                .HasColumnName("AverageRating");
            modelBuilder.Entity<OnlineOrder>().ToTable("OnlineOrders");

            // Inventory management entity mappings
            modelBuilder.Entity<InventoryStock>().ToTable("InventoryStocks");
            modelBuilder.Entity<InventoryMovement>().ToTable("InventoryMovements");
            modelBuilder.Entity<InventorySupplier>().ToTable("InventorySuppliers");
            modelBuilder.Entity<ArticleSupplier>().ToTable("ArticleSuppliers");
            modelBuilder.Entity<ReorderSuggestion>().ToTable("ReorderSuggestions");
            modelBuilder.Entity<StockAlert>().ToTable("StockAlerts");

            // Customer loyalty entity mappings
            modelBuilder.Entity<Customer>().ToTable("Customers");
            modelBuilder.Entity<LoyaltyTransaction>().ToTable("LoyaltyTransactions");
            modelBuilder.Entity<Campaign>().ToTable("Campaigns");
            modelBuilder.Entity<CampaignUsage>().ToTable("CampaignUsages");
            modelBuilder.Entity<CampaignUsage>()
                .Property(c => c.BenefitAmount)
                .HasColumnName("DiscountAmount");
            modelBuilder.Entity<CustomerOrderHistory>().ToTable("CustomerOrderHistories");
            modelBuilder.Entity<CustomerOrderHistory>()
                .Property(c => c.OrderTotal)
                .HasColumnName("TotalAmount");
            modelBuilder.Entity<CustomerOrderHistory>()
                .Property(c => c.OrderItems)
                .HasColumnName("OrderItems")
                .IsRequired(false);

            // Staff scheduling entity mappings
            modelBuilder.Entity<WorkShift>().ToTable("WorkShifts");
            modelBuilder.Entity<EmployeePerformance>().ToTable("EmployeePerformances");
            modelBuilder.Entity<TimeCard>().ToTable("TimeCards");
            modelBuilder.Entity<LaborCostAnalysis>().ToTable("LaborCostAnalyses");
            modelBuilder.Entity<EmployeeAvailability>().ToTable("EmployeeAvailabilities");
            modelBuilder.Entity<TimeOffRequest>().ToTable("TimeOffRequests");

            // Kitchen display system entity mappings
            modelBuilder.Entity<KitchenOrder>().ToTable("KitchenOrders");
            modelBuilder.Entity<KitchenOrderItem>().ToTable("KitchenOrderItems");
            modelBuilder.Entity<KitchenOrderItem>()
                .Property(k => k.ItemName)
                .HasColumnName("ArticleName");
            modelBuilder.Entity<IngredientPrep>().ToTable("IngredientPreps");
            modelBuilder.Entity<KitchenStation>().ToTable("KitchenStations");
            modelBuilder.Entity<KitchenPerformance>().ToTable("KitchenPerformances");
        }
    }
}
