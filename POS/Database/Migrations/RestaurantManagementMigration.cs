using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using KosovaPOS.Database;

namespace KosovaPOS.Database.Migrations
{
    /// <summary>
    /// Migration runner to ensure all restaurant management tables exist
    /// Creates tables for: Restaurant Tables, Kitchen Orders, Inventory, Loyalty, Staff, etc.
    /// </summary>
    public class RestaurantManagementMigration
    {
        public static async Task<bool> EnsureTablesExistAsync()
        {
            try
            {
                var server = Environment.GetEnvironmentVariable("SQL_SERVER") ?? "(localdb)\\MSSQLLocalDB";
                var database = Environment.GetEnvironmentVariable("SQL_DATABASE") ?? "BMDData";
                var connectionString = $"Server={server};Database={database};Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;";
                
                using var context = new POSDbContext();
                
                // Test connection first
                if (!await context.Database.CanConnectAsync())
                {
                    Console.WriteLine("Cannot connect to database");
                    return false;
                }

                // Apply pending migrations using EF Core
                var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    Console.WriteLine("Applying pending migrations...");
                    await context.Database.MigrateAsync();
                    Console.WriteLine("Migrations applied successfully");
                }

                // Ensure all critical tables exist
                await EnsureTableExistsAsync(connectionString, "PizzaCategories", CreatePizzaCategoriesTable);
                await EnsureTableExistsAsync(connectionString, "PizzaToppings", CreatePizzaToppingsTable);
                await EnsureTableExistsAsync(connectionString, "RestaurantTables", CreateRestaurantTablesTable);
                await EnsureTableExistsAsync(connectionString, "TableReservations", CreateTableReservationsTable);
                await EnsureTableExistsAsync(connectionString, "DeliveryOrders", CreateDeliveryOrdersTable);
                await EnsureTableExistsAsync(connectionString, "DeliveryDrivers", CreateDeliveryDriversTable);
                await EnsureTableExistsAsync(connectionString, "OnlineOrders", CreateOnlineOrdersTable);
                
                await EnsureTableExistsAsync(connectionString, "KitchenOrders", CreateKitchenOrdersTable);
                await EnsureTableExistsAsync(connectionString, "KitchenOrderItems", CreateKitchenOrderItemsTable);
                await EnsureTableExistsAsync(connectionString, "IngredientPreps", CreateIngredientPrepsTable);
                await EnsureTableExistsAsync(connectionString, "KitchenStations", CreateKitchenStationsTable);
                await EnsureTableExistsAsync(connectionString, "KitchenPerformances", CreateKitchenPerformancesTable);
                
                await EnsureTableExistsAsync(connectionString, "Customers", CreateCustomersTable);
                await EnsureTableExistsAsync(connectionString, "LoyaltyTransactions", CreateLoyaltyTransactionsTable);
                await EnsureTableExistsAsync(connectionString, "Campaigns", CreateCampaignsTable);
                await EnsureTableExistsAsync(connectionString, "CampaignUsages", CreateCampaignUsagesTable);
                await EnsureTableExistsAsync(connectionString, "CustomerOrderHistories", CreateCustomerOrderHistoriesTable);
                
                await EnsureTableExistsAsync(connectionString, "InventoryStocks", CreateInventoryStocksTable);
                await EnsureTableExistsAsync(connectionString, "InventoryMovements", CreateInventoryMovementsTable);
                await EnsureTableExistsAsync(connectionString, "InventorySuppliers", CreateInventorySuppliersTable);
                await EnsureTableExistsAsync(connectionString, "ArticleSuppliers", CreateArticleSuppliersTable);
                await EnsureTableExistsAsync(connectionString, "ReorderSuggestions", CreateReorderSuggestionsTable);
                await EnsureTableExistsAsync(connectionString, "StockAlerts", CreateStockAlertsTable);
                
                await EnsureTableExistsAsync(connectionString, "WorkShifts", CreateWorkShiftsTable);
                await EnsureTableExistsAsync(connectionString, "StaffMembers", CreateStaffMembersTable);
                await EnsureTableExistsAsync(connectionString, "EmployeePerformances", CreateEmployeePerformancesTable);
                await EnsureTableExistsAsync(connectionString, "TimeCards", CreateTimeCardsTable);
                await EnsureTableExistsAsync(connectionString, "LaborCostAnalyses", CreateLaborCostAnalysesTable);
                await EnsureTableExistsAsync(connectionString, "EmployeeAvailabilities", CreateEmployeeAvailabilitiesTable);
                await EnsureTableExistsAsync(connectionString, "TimeOffRequests", CreateTimeOffRequestsTable);

                // Ensure missing columns exist in tables that may have been created with older schema
                await EnsureColumnExistsAsync(connectionString, "RestaurantTables", "ReservedFor", "NVARCHAR(100) NULL");
                await EnsureColumnExistsAsync(connectionString, "RestaurantTables", "ReservedPhone", "NVARCHAR(20) NULL");
                await EnsureColumnExistsAsync(connectionString, "RestaurantTables", "ReservedUntil", "DATETIME2 NULL");
                await EnsureColumnExistsAsync(connectionString, "RestaurantTables", "OccupiedSince", "DATETIME2 NULL");
                await EnsureColumnExistsAsync(connectionString, "RestaurantTables", "CurrentReceiptId", "INT NULL");
                await EnsureColumnExistsAsync(connectionString, "RestaurantTables", "Location", "NVARCHAR(50) NULL");

                // Artikujt table - add new columns for IsActive and MinimumStock if they don't exist
                await EnsureColumnExistsAsync(connectionString, "Artikujt", "IsActive", "BIT NULL DEFAULT 1");
                await EnsureColumnExistsAsync(connectionString, "Artikujt", "StoguMinimal", "FLOAT NULL DEFAULT 0");

                await EnsureColumnExistsAsync(connectionString, "InventoryMovements", "ArticleName", "NVARCHAR(100) NULL");
                await EnsureColumnExistsAsync(connectionString, "InventoryMovements", "Unit", "NVARCHAR(20) NULL");
                await EnsureColumnExistsAsync(connectionString, "InventoryMovements", "Description", "NVARCHAR(500) NULL");
                await EnsureColumnExistsAsync(connectionString, "InventoryMovements", "Reference", "NVARCHAR(50) NULL");
                await EnsureColumnExistsAsync(connectionString, "InventoryMovements", "MovementDate", "DATETIME2 NOT NULL DEFAULT GETDATE()");

                await EnsureColumnExistsAsync(connectionString, "WorkShifts", "ClockInTime", "DATETIME2 NULL");
                await EnsureColumnExistsAsync(connectionString, "WorkShifts", "ClockOutTime", "DATETIME2 NULL");
                await EnsureColumnExistsAsync(connectionString, "WorkShifts", "BreakMinutes", "INT NOT NULL DEFAULT 0");
                await EnsureColumnExistsAsync(connectionString, "WorkShifts", "Position", "NVARCHAR(50) NULL");
                await EnsureColumnExistsAsync(connectionString, "WorkShifts", "HourlyRate", "DECIMAL(18,2) NOT NULL DEFAULT 0");
                await EnsureColumnExistsAsync(connectionString, "WorkShifts", "HoursWorked", "DECIMAL(18,2) NOT NULL DEFAULT 0");
                await EnsureColumnExistsAsync(connectionString, "WorkShifts", "LaborCost", "DECIMAL(18,2) NOT NULL DEFAULT 0");
                await EnsureColumnExistsAsync(connectionString, "WorkShifts", "UpdatedAt", "DATETIME2 NOT NULL DEFAULT GETDATE()");

                await EnsureColumnExistsAsync(connectionString, "TimeCards", "Date", "DATE NOT NULL DEFAULT CAST(GETDATE() AS DATE)");
                await EnsureColumnExistsAsync(connectionString, "TimeCards", "ClockInTime", "DATETIME2 NULL");
                await EnsureColumnExistsAsync(connectionString, "TimeCards", "ClockOutTime", "DATETIME2 NULL");
                await EnsureColumnExistsAsync(connectionString, "TimeCards", "BreakStartTime", "DATETIME2 NULL");
                await EnsureColumnExistsAsync(connectionString, "TimeCards", "BreakEndTime", "DATETIME2 NULL");
                await EnsureColumnExistsAsync(connectionString, "TimeCards", "TotalBreakMinutes", "INT NOT NULL DEFAULT 0");
                await EnsureColumnExistsAsync(connectionString, "TimeCards", "ShiftId", "INT NULL");

                await EnsureColumnExistsAsync(connectionString, "Campaigns", "CampaignType", "NVARCHAR(50) NOT NULL DEFAULT 'Discount'");
                await EnsureColumnExistsAsync(connectionString, "Campaigns", "Status", "NVARCHAR(20) NOT NULL DEFAULT 'Draft'");
                await EnsureColumnExistsAsync(connectionString, "Campaigns", "Value", "DECIMAL(18,2) NOT NULL DEFAULT 0");
                await EnsureColumnExistsAsync(connectionString, "Campaigns", "UsageCount", "INT NOT NULL DEFAULT 0");
                await EnsureColumnExistsAsync(connectionString, "Campaigns", "TotalRevenue", "DECIMAL(18,2) NOT NULL DEFAULT 0");
                await EnsureColumnExistsAsync(connectionString, "Campaigns", "PointsRequired", "INT NOT NULL DEFAULT 0");
                await EnsureColumnExistsAsync(connectionString, "Campaigns", "DiscountAmount", "DECIMAL(18,2) NOT NULL DEFAULT 0");
                await EnsureColumnExistsAsync(connectionString, "Campaigns", "UpdatedAt", "DATETIME2 NOT NULL DEFAULT GETDATE()");
                await EnsureColumnExistsAsync(connectionString, "Campaigns", "ApplicableArticles", "NVARCHAR(MAX) NULL");
                await EnsureColumnExistsAsync(connectionString, "Campaigns", "MinimumPurchase", "DECIMAL(18,2) NULL");
                await EnsureColumnExistsAsync(connectionString, "Campaigns", "TargetTier", "NVARCHAR(20) NULL");
                await EnsureColumnExistsAsync(connectionString, "Campaigns", "IsActive", "BIT NOT NULL DEFAULT 1");

                // Fix: old Campaigns table created with NOT NULL columns not in EF model → make them nullable
                await EnsureColumnNullableAsync(connectionString, "Campaigns", "Type", "NVARCHAR(50)");
                await EnsureColumnNullableAsync(connectionString, "Campaigns", "DiscountType", "NVARCHAR(20)");
                await EnsureColumnNullableAsync(connectionString, "Campaigns", "DiscountValue", "DECIMAL(18,2)");
                await EnsureColumnNullableAsync(connectionString, "Campaigns", "MaxUsePerCustomer", "INT");

                // Fix: KitchenOrders schema mismatch — detect and recreate if needed
                await EnsureKitchenOrdersSchemaAsync(connectionString);

                // Ensure all KitchenOrders columns exist for new/recreated tables
                await EnsureColumnExistsAsync(connectionString, "KitchenOrders", "CustomerName", "NVARCHAR(100) NULL");
                await EnsureColumnExistsAsync(connectionString, "KitchenOrders", "OrderItems", "NVARCHAR(MAX) NULL");
                await EnsureColumnExistsAsync(connectionString, "KitchenOrders", "SpecialInstructions", "NVARCHAR(1000) NULL");
                await EnsureColumnExistsAsync(connectionString, "KitchenOrders", "Station", "NVARCHAR(50) NULL");
                await EnsureColumnExistsAsync(connectionString, "KitchenOrders", "AssignedTo", "NVARCHAR(100) NULL");
                await EnsureColumnExistsAsync(connectionString, "KitchenOrders", "ReceivedAt", "DATETIME2 NOT NULL DEFAULT GETDATE()");
                await EnsureColumnExistsAsync(connectionString, "KitchenOrders", "StartedAt", "DATETIME2 NULL");
                await EnsureColumnExistsAsync(connectionString, "KitchenOrders", "ReadyAt", "DATETIME2 NULL");
                await EnsureColumnExistsAsync(connectionString, "KitchenOrders", "ServedAt", "DATETIME2 NULL");
                await EnsureColumnExistsAsync(connectionString, "KitchenOrders", "TargetTime", "INT NOT NULL DEFAULT 15");
                await EnsureColumnExistsAsync(connectionString, "KitchenOrders", "ActualTime", "INT NULL");
                await EnsureColumnExistsAsync(connectionString, "KitchenOrders", "CreatedAt", "DATETIME2 NOT NULL DEFAULT GETDATE()");
                await EnsureColumnExistsAsync(connectionString, "KitchenOrders", "UpdatedAt", "DATETIME2 NOT NULL DEFAULT GETDATE()");
                await EnsureColumnNullableAsync(connectionString, "KitchenOrders", "Priority", "NVARCHAR(20)");

                // Ensure KitchenOrderItems has all EF model columns
                await EnsureColumnExistsAsync(connectionString, "KitchenOrderItems", "ArticleId", "INT NOT NULL DEFAULT 0");
                await EnsureColumnExistsAsync(connectionString, "KitchenOrderItems", "Modifications", "NVARCHAR(MAX) NULL");
                await EnsureColumnExistsAsync(connectionString, "KitchenOrderItems", "Station", "NVARCHAR(50) NULL");
                await EnsureColumnExistsAsync(connectionString, "KitchenOrderItems", "SequenceNumber", "INT NOT NULL DEFAULT 0");
                await EnsureColumnExistsAsync(connectionString, "KitchenOrderItems", "UpdatedAt", "DATETIME2 NOT NULL DEFAULT GETDATE()");
                await EnsureColumnNullableAsync(connectionString, "KitchenOrderItems", "ArticleName", "NVARCHAR(200)");
                await EnsureColumnNullableAsync(connectionString, "KitchenOrderItems", "Status", "NVARCHAR(20)");
                // Ensure Quantity is INT (model) — if it is DECIMAL cast is handled by EF
                await EnsureColumnExistsAsync(connectionString, "KitchenOrderItems", "CreatedAt", "DATETIME2 NOT NULL DEFAULT GETDATE()");

                await EnsureColumnExistsAsync(connectionString, "LoyaltyTransactions", "CustomerName", "NVARCHAR(100) NOT NULL DEFAULT ''");
                await EnsureColumnExistsAsync(connectionString, "LoyaltyTransactions", "Amount", "DECIMAL(18,2) NOT NULL DEFAULT 0");
                await EnsureColumnExistsAsync(connectionString, "LoyaltyTransactions", "TransactionDate", "DATETIME2 NOT NULL DEFAULT GETDATE()");
                await EnsureColumnExistsAsync(connectionString, "LoyaltyTransactions", "ReferenceNumber", "NVARCHAR(50) NULL");
                await EnsureColumnExistsAsync(connectionString, "LoyaltyTransactions", "PurchaseAmount", "DECIMAL(18,2) NULL");
                await EnsureColumnExistsAsync(connectionString, "LoyaltyTransactions", "CreatedAt", "DATETIME2 NOT NULL DEFAULT GETDATE()");
                await EnsureColumnNullableAsync(connectionString, "LoyaltyTransactions", "CreatedBy", "NVARCHAR(100)");

                // Customers – store credit / wallet balance
                await EnsureColumnExistsAsync(connectionString, "Customers", "StoreCredit", "DECIMAL(18,2) NOT NULL DEFAULT 0");

                // CustomerOrderHistories - ensure EF-model columns exist
                await EnsureColumnExistsAsync(connectionString, "CustomerOrderHistories", "OrderItems", "NVARCHAR(MAX) NULL");
                // EF model uses OrderTotal but old table may have TotalAmount - add OrderTotal if missing
                await EnsureColumnExistsAsync(connectionString, "CustomerOrderHistories", "TotalAmount", "DECIMAL(18,2) NOT NULL DEFAULT 0");

                // CampaignUsages - ensure EF-model BenefitAmount maps to DiscountAmount (done via Fluent API, but ensure column exists)
                await EnsureColumnExistsAsync(connectionString, "CampaignUsages", "DiscountAmount", "DECIMAL(18,2) NOT NULL DEFAULT 0");

                await EnsureColumnExistsAsync(connectionString, "StockAlerts", "Message", "NVARCHAR(500) NOT NULL DEFAULT ''");
                await EnsureColumnExistsAsync(connectionString, "StockAlerts", "ThresholdQuantity", "DECIMAL(18,3) NOT NULL DEFAULT 0");

                // Fix DeliveryOrders schema - add columns present in model but missing from original CREATE TABLE
                await EnsureColumnExistsAsync(connectionString, "DeliveryOrders", "DriverName", "NVARCHAR(100) NULL");
                await EnsureColumnExistsAsync(connectionString, "DeliveryOrders", "AcceptedAt", "DATETIME2 NULL");
                await EnsureColumnExistsAsync(connectionString, "DeliveryOrders", "PickedUpAt", "DATETIME2 NULL");
                await EnsureColumnExistsAsync(connectionString, "DeliveryOrders", "DeliveredAt", "DATETIME2 NULL");
                await EnsureColumnExistsAsync(connectionString, "DeliveryOrders", "Rating", "INT NULL");
                await EnsureColumnExistsAsync(connectionString, "DeliveryOrders", "CustomerFeedback", "NVARCHAR(500) NULL");
                await EnsureColumnExistsAsync(connectionString, "DeliveryOrders", "EstDeliveryMinutes", "INT NOT NULL DEFAULT 30");

                // Fix DeliveryDrivers schema - add columns present in model but missing from original CREATE TABLE
                await EnsureColumnExistsAsync(connectionString, "DeliveryDrivers", "ActiveDeliveries", "INT NOT NULL DEFAULT 0");
                await EnsureColumnExistsAsync(connectionString, "DeliveryDrivers", "UpdatedAt", "DATETIME2 NOT NULL DEFAULT GETDATE()");
                await EnsureColumnExistsAsync(connectionString, "DeliveryDrivers", "AverageRating", "DECIMAL(18,2) NOT NULL DEFAULT 0");

                // Fix TimeCards schema - original CREATE TABLE has NOT NULL Status/ClockIn columns not in EF model
                await EnsureColumnNullableAsync(connectionString, "TimeCards", "Status", "NVARCHAR(20)");
                await EnsureColumnNullableAsync(connectionString, "TimeCards", "ClockIn", "DATETIME2");

                // Fix DeliveryOrders.OrderNumber - ensure existing rows have a non-empty value and column allows a default
                await EnsureDeliveryOrderNumberDefaultAsync(connectionString);

                // Fix POSUsers_Legacy - ensure table and all User-model permission columns exist
                await EnsureTableExistsAsync(connectionString, "POSUsers_Legacy", CreatePOSUsersLegacyTable);
                await EnsureColumnExistsAsync(connectionString, "POSUsers_Legacy", "CanManageDelivery", "BIT NOT NULL DEFAULT 0");
                await EnsureColumnExistsAsync(connectionString, "POSUsers_Legacy", "CanManageKitchen", "BIT NOT NULL DEFAULT 0");
                await EnsureColumnExistsAsync(connectionString, "POSUsers_Legacy", "CanManageTables", "BIT NOT NULL DEFAULT 0");
                await EnsureColumnExistsAsync(connectionString, "POSUsers_Legacy", "CanManageLoyalty", "BIT NOT NULL DEFAULT 0");
                await EnsureColumnExistsAsync(connectionString, "POSUsers_Legacy", "CanViewAnalytics", "BIT NOT NULL DEFAULT 1");
                await EnsureColumnExistsAsync(connectionString, "POSUsers_Legacy", "CanManageInventory", "BIT NOT NULL DEFAULT 0");
                await EnsureColumnExistsAsync(connectionString, "POSUsers_Legacy", "CanAccessStaffScheduling", "BIT NOT NULL DEFAULT 0");

                Console.WriteLine("✓ All restaurant management tables verified/created successfully");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error ensuring tables exist: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        private static async Task<bool> EnsureTableExistsAsync(string connectionString, string tableName, Func<string> createTableSql)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Check if table exists
                var checkSql = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}'";
                using var checkCommand = new SqlCommand(checkSql, connection);
                var exists = (int)await checkCommand.ExecuteScalarAsync() > 0;

                if (!exists)
                {
                    Console.WriteLine($"Creating table: {tableName}");
                    var sql = createTableSql();
                    using var createCommand = new SqlCommand(sql, connection);
                    createCommand.CommandTimeout = 120;
                    await createCommand.ExecuteNonQueryAsync();
                    Console.WriteLine($"✓ Created table: {tableName}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error ensuring table {tableName} exists: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> EnsureColumnExistsAsync(string connectionString, string tableName, string columnName, string columnDefinition)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var tableCheckSql = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}'";
                using var tableCheckCmd = new SqlCommand(tableCheckSql, connection);
                if ((int)await tableCheckCmd.ExecuteScalarAsync() == 0) return true;

                var checkSql = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = '{columnName}'";
                using var checkCommand = new SqlCommand(checkSql, connection);
                var exists = (int)await checkCommand.ExecuteScalarAsync() > 0;

                if (!exists)
                {
                    Console.WriteLine($"Adding column {columnName} to {tableName}");
                    var alterSql = $"ALTER TABLE [{tableName}] ADD [{columnName}] {columnDefinition}";
                    using var alterCommand = new SqlCommand(alterSql, connection);
                    alterCommand.CommandTimeout = 60;
                    await alterCommand.ExecuteNonQueryAsync();
                    Console.WriteLine($"✓ Added column {columnName} to {tableName}");
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error ensuring column {columnName} in {tableName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Makes an existing NOT NULL column nullable so EF Core INSERT without that column succeeds.
        /// </summary>
        private static async Task<bool> EnsureColumnNullableAsync(string connectionString, string tableName, string columnName, string dataType)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Only proceed if table + column exist and column is currently NOT NULL
                var checkSql = $@"
                    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = '{columnName}'
                      AND IS_NULLABLE = 'NO'";
                using var checkCmd = new SqlCommand(checkSql, connection);
                if ((int)await checkCmd.ExecuteScalarAsync() == 0) return true;

                // Drop any default constraint on the column first
                var dropDefaultSql = $@"
                    DECLARE @constraintName NVARCHAR(256)
                    SELECT @constraintName = dc.name
                    FROM sys.default_constraints dc
                    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
                    WHERE dc.parent_object_id = OBJECT_ID('{tableName}') AND c.name = '{columnName}'
                    IF @constraintName IS NOT NULL
                        EXEC('ALTER TABLE [{tableName}] DROP CONSTRAINT [' + @constraintName + ']')";
                using var dropCmd = new SqlCommand(dropDefaultSql, connection);
                dropCmd.CommandTimeout = 60;
                await dropCmd.ExecuteNonQueryAsync();

                var alterSql = $"ALTER TABLE [{tableName}] ALTER COLUMN [{columnName}] {dataType} NULL";
                using var alterCmd = new SqlCommand(alterSql, connection);
                alterCmd.CommandTimeout = 60;
                await alterCmd.ExecuteNonQueryAsync();
                Console.WriteLine($"✓ Made column {columnName} in {tableName} nullable");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error making column {columnName} nullable in {tableName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Drops and recreates the KitchenOrders table when its schema is incompatible with the KitchenOrder model.
        /// This is safe because kitchen orders are transient operational data.
        /// </summary>
        private static async Task EnsureKitchenOrdersSchemaAsync(string connectionString)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Check if table exists at all
                var tableCheck = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'KitchenOrders'";
                using var tableCheckCmd = new SqlCommand(tableCheck, connection);
                if ((int)await tableCheckCmd.ExecuteScalarAsync() == 0) return; // Will be created by EnsureTableExistsAsync

                // Check for the model-required INT column OrderNumber
                var typeCheckSql = $@"
                    SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'KitchenOrders' AND COLUMN_NAME = 'OrderNumber'";
                using var typeCheckCmd = new SqlCommand(typeCheckSql, connection);
                var orderNumberType = await typeCheckCmd.ExecuteScalarAsync() as string;

                bool needsRecreate = false;

                // If OrderNumber is not INT, schema is old → recreate
                if (orderNumberType != null && orderNumberType.ToLower() != "int")
                    needsRecreate = true;

                // Also check if TargetTime column exists (missing in old schema)
                var targetTimeCheck = $@"
                    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'KitchenOrders' AND COLUMN_NAME = 'TargetTime'";
                using var targetTimeCmd = new SqlCommand(targetTimeCheck, connection);
                if ((int)await targetTimeCmd.ExecuteScalarAsync() == 0)
                    needsRecreate = true;

                // Also check if CreatedAt column exists (missing in old schema causes EF Core save error)
                var createdAtCheck = $@"
                    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'KitchenOrders' AND COLUMN_NAME = 'CreatedAt'";
                using var createdAtCmd = new SqlCommand(createdAtCheck, connection);
                if ((int)await createdAtCmd.ExecuteScalarAsync() == 0)
                    needsRecreate = true;

                if (needsRecreate)
                {
                    Console.WriteLine("KitchenOrders schema is outdated — recreating tables...");

                    // Drop KitchenOrderItems first (has FK referencing KitchenOrders)
                    var dropChildSql = @"
                        IF OBJECT_ID('KitchenOrderItems', 'U') IS NOT NULL
                        BEGIN
                            DECLARE @fkName NVARCHAR(256)
                            SELECT TOP 1 @fkName = fk.name
                            FROM sys.foreign_keys fk
                            JOIN sys.tables t ON fk.parent_object_id = t.object_id
                            WHERE t.name = 'KitchenOrderItems'
                            IF @fkName IS NOT NULL
                                EXEC('ALTER TABLE [KitchenOrderItems] DROP CONSTRAINT [' + @fkName + ']')
                            DROP TABLE [KitchenOrderItems]
                        END";
                    using var dropChildCmd = new SqlCommand(dropChildSql, connection);
                    dropChildCmd.CommandTimeout = 60;
                    await dropChildCmd.ExecuteNonQueryAsync();

                    var dropSql = "DROP TABLE [KitchenOrders]";
                    using var dropCmd = new SqlCommand(dropSql, connection);
                    dropCmd.CommandTimeout = 60;
                    await dropCmd.ExecuteNonQueryAsync();

                    var createSql = CreateKitchenOrdersTable();
                    using var createCmd = new SqlCommand(createSql, connection);
                    createCmd.CommandTimeout = 120;
                    await createCmd.ExecuteNonQueryAsync();

                    // Recreate KitchenOrderItems table
                    var createItemsSql = CreateKitchenOrderItemsTable();
                    using var createItemsCmd = new SqlCommand(createItemsSql, connection);
                    createItemsCmd.CommandTimeout = 120;
                    await createItemsCmd.ExecuteNonQueryAsync();

                    Console.WriteLine("✓ KitchenOrders and KitchenOrderItems tables recreated with correct schema");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error checking/fixing KitchenOrders schema: {ex.Message}");
            }
        }

        // Table creation SQL statements

        private static string CreateRestaurantTablesTable() => @"
CREATE TABLE RestaurantTables (
    Id INT PRIMARY KEY IDENTITY(1,1),
    TableNumber NVARCHAR(20) NOT NULL,
    Capacity INT NOT NULL,
    Section NVARCHAR(50),
    Status NVARCHAR(20) NOT NULL DEFAULT 'Available',
    CurrentOrderId INT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);";

        private static string CreateTableReservationsTable() => @"
CREATE TABLE TableReservations (
    Id INT PRIMARY KEY IDENTITY(1,1),
    TableId INT NOT NULL,
    CustomerName NVARCHAR(100) NOT NULL,
    CustomerPhone NVARCHAR(20),
    PartySize INT NOT NULL,
    ReservationDate DATETIME2 NOT NULL,
    Duration INT NOT NULL,
    Status NVARCHAR(20) NOT NULL,
    Notes NVARCHAR(500),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    CreatedBy NVARCHAR(100)
);";

        private static string CreateDeliveryOrdersTable() => @"
CREATE TABLE DeliveryOrders (
    Id INT PRIMARY KEY IDENTITY(1,1),
    OrderNumber NVARCHAR(50) NOT NULL,
    ReceiptId INT NULL,
    CustomerName NVARCHAR(100) NOT NULL,
    CustomerPhone NVARCHAR(20) NOT NULL,
    DeliveryAddress NVARCHAR(500) NOT NULL,
    DriverId INT NULL,
    Status NVARCHAR(20) NOT NULL,
    TotalAmount DECIMAL(18,2) NOT NULL,
    DeliveryFee DECIMAL(18,2) NOT NULL DEFAULT 0,
    EstimatedDeliveryTime DATETIME2,
    ActualDeliveryTime DATETIME2,
    OrderSource NVARCHAR(20),
    PaymentMethod NVARCHAR(50),
    PaymentStatus NVARCHAR(20),
    Notes NVARCHAR(500),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);";

        private static string CreateDeliveryDriversTable() => @"
CREATE TABLE DeliveryDrivers (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(100) NOT NULL,
    Phone NVARCHAR(20) NOT NULL,
    VehicleType NVARCHAR(50),
    VehiclePlate NVARCHAR(20),
    Status NVARCHAR(20) NOT NULL DEFAULT 'Available',
    CurrentDeliveryId INT NULL,
    TotalDeliveries INT NOT NULL DEFAULT 0,
    Rating DECIMAL(3,2) NOT NULL DEFAULT 5.00,
    IsActive BIT NOT NULL DEFAULT 1,
    HiredDate DATETIME2,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);";

        private static string CreateOnlineOrdersTable() => @"
CREATE TABLE OnlineOrders (
    Id INT PRIMARY KEY IDENTITY(1,1),
    OrderNumber NVARCHAR(50) NOT NULL,
    Platform NVARCHAR(50) NOT NULL,
    ExternalOrderId NVARCHAR(100),
    CustomerName NVARCHAR(100) NOT NULL,
    CustomerPhone NVARCHAR(20),
    Status NVARCHAR(20) NOT NULL,
    TotalAmount DECIMAL(18,2) NOT NULL,
    PlatformFee DECIMAL(18,2) NOT NULL DEFAULT 0,
    OrderType NVARCHAR(20) NOT NULL,
    DeliveryOrderId INT NULL,
    JsonData NVARCHAR(MAX),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    ProcessedAt DATETIME2,
    CompletedAt DATETIME2
);";

        private static string CreateKitchenOrdersTable() => @"
CREATE TABLE KitchenOrders (
    Id INT PRIMARY KEY IDENTITY(1,1),
    ReceiptId INT NOT NULL DEFAULT 0,
    OrderNumber INT NOT NULL DEFAULT 0,
    OrderType NVARCHAR(20) NOT NULL DEFAULT 'DineIn',
    TableNumber NVARCHAR(20) NULL,
    CustomerName NVARCHAR(100) NULL,
    OrderItems NVARCHAR(MAX) NULL,
    SpecialInstructions NVARCHAR(1000) NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'New',
    Priority NVARCHAR(20) NOT NULL DEFAULT 'Normal',
    Station NVARCHAR(50) NULL,
    AssignedTo NVARCHAR(100) NULL,
    ReceivedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    StartedAt DATETIME2 NULL,
    ReadyAt DATETIME2 NULL,
    ServedAt DATETIME2 NULL,
    TargetTime INT NOT NULL DEFAULT 15,
    ActualTime INT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);";

        private static string CreateKitchenOrderItemsTable() => @"
CREATE TABLE KitchenOrderItems (
    Id INT PRIMARY KEY IDENTITY(1,1),
    KitchenOrderId INT NOT NULL,
    ArticleId INT NOT NULL DEFAULT 0,
    ArticleName NVARCHAR(200) NOT NULL DEFAULT '',
    Quantity INT NOT NULL DEFAULT 1,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    Modifications NVARCHAR(MAX) NULL,
    SpecialInstructions NVARCHAR(500) NULL,
    Station NVARCHAR(50) NULL,
    SequenceNumber INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    FOREIGN KEY (KitchenOrderId) REFERENCES KitchenOrders(Id) ON DELETE CASCADE
);";

        private static string CreateIngredientPrepsTable() => @"
CREATE TABLE IngredientPreps (
    Id INT PRIMARY KEY IDENTITY(1,1),
    IngredientName NVARCHAR(100) NOT NULL,
    PreparedQuantity DECIMAL(18,3) NOT NULL,
    Unit NVARCHAR(20) NOT NULL,
    ExpiryTime DATETIME2 NOT NULL,
    PreparedBy NVARCHAR(100),
    StationId INT NULL,
    Status NVARCHAR(20) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);";

        private static string CreateKitchenStationsTable() => @"
CREATE TABLE KitchenStations (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(100) NOT NULL,
    Type NVARCHAR(50) NOT NULL,
    DisplayOrder INT NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);";

        private static string CreateKitchenPerformancesTable() => @"
CREATE TABLE KitchenPerformances (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Date DATE NOT NULL,
    TotalOrders INT NOT NULL DEFAULT 0,
    CompletedOrders INT NOT NULL DEFAULT 0,
    AveragePrepTime DECIMAL(18,2) NOT NULL DEFAULT 0,
    OrdersOnTime INT NOT NULL DEFAULT 0,
    OrdersLate INT NOT NULL DEFAULT 0,
    PeakHourVolume INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);";

        private static string CreateCustomersTable() => @"
CREATE TABLE Customers (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(100) NOT NULL,
    Phone NVARCHAR(20),
    Email NVARCHAR(100),
    LoyaltyCardNumber NVARCHAR(50),
    LoyaltyPoints INT NOT NULL DEFAULT 0,
    TotalPointsEarned INT NOT NULL DEFAULT 0,
    TotalPointsRedeemed INT NOT NULL DEFAULT 0,
    Tier NVARCHAR(20) NOT NULL DEFAULT 'Bronze',
    TotalSpent DECIMAL(18,2) NOT NULL DEFAULT 0,
    OrderCount INT NOT NULL DEFAULT 0,
    Birthday DATETIME2,
    PreferredContact NVARCHAR(20),
    Address NVARCHAR(500),
    City NVARCHAR(100),
    Preferences NVARCHAR(MAX),
    MarketingOptIn BIT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    LastVisit DATETIME2
);";

        private static string CreateLoyaltyTransactionsTable() => @"
CREATE TABLE LoyaltyTransactions (
    Id INT PRIMARY KEY IDENTITY(1,1),
    CustomerId INT NOT NULL,
    TransactionType NVARCHAR(20) NOT NULL,
    Points INT NOT NULL,
    ReceiptId INT NULL,
    Description NVARCHAR(500),
    TransactionDate DATETIME2 NOT NULL DEFAULT GETDATE(),
    CreatedBy NVARCHAR(100),
    FOREIGN KEY (CustomerId) REFERENCES Customers(Id) ON DELETE CASCADE
);";

        private static string CreateCampaignsTable() => @"
CREATE TABLE Campaigns (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    CampaignType NVARCHAR(50) NOT NULL DEFAULT 'Discount',
    PointsRequired INT NOT NULL DEFAULT 0,
    DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    Value DECIMAL(18,2) NOT NULL DEFAULT 0,
    TargetTier NVARCHAR(20) NULL,
    MinimumPurchase DECIMAL(18,2) NULL,
    ApplicableArticles NVARCHAR(MAX) NULL,
    StartDate DATETIME2 NOT NULL,
    EndDate DATETIME2 NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Draft',
    UsageCount INT NOT NULL DEFAULT 0,
    TotalRevenue DECIMAL(18,2) NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);";

        private static string CreateCampaignUsagesTable() => @"
CREATE TABLE CampaignUsages (
    Id INT PRIMARY KEY IDENTITY(1,1),
    CampaignId INT NOT NULL,
    CustomerId INT NOT NULL,
    ReceiptId INT NULL,
    DiscountAmount DECIMAL(18,2) NOT NULL,
    UsedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    FOREIGN KEY (CampaignId) REFERENCES Campaigns(Id) ON DELETE CASCADE,
    FOREIGN KEY (CustomerId) REFERENCES Customers(Id) ON DELETE CASCADE
);";

        private static string CreateCustomerOrderHistoriesTable() => @"
CREATE TABLE CustomerOrderHistories (
    Id INT PRIMARY KEY IDENTITY(1,1),
    CustomerId INT NOT NULL,
    ReceiptId INT NOT NULL,
    OrderDate DATETIME2 NOT NULL,
    TotalAmount DECIMAL(18,2) NOT NULL,
    PointsEarned INT NOT NULL DEFAULT 0,
    PaymentMethod NVARCHAR(50),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    FOREIGN KEY (CustomerId) REFERENCES Customers(Id) ON DELETE CASCADE
);";

        private static string CreateInventoryStocksTable() => @"
CREATE TABLE InventoryStocks (
    Id INT PRIMARY KEY IDENTITY(1,1),
    ArticleId INT NOT NULL,
    ArticleName NVARCHAR(100) NOT NULL,
    CurrentQuantity DECIMAL(18,3) NOT NULL,
    MinimumQuantity DECIMAL(18,3) NOT NULL,
    ReorderQuantity DECIMAL(18,3) NOT NULL,
    Unit NVARCHAR(20) NOT NULL,
    LastRestockedAt DATETIME2,
    Location NVARCHAR(100),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);";

        private static string CreateInventoryMovementsTable() => @"
CREATE TABLE InventoryMovements (
    Id INT PRIMARY KEY IDENTITY(1,1),
    InventoryStockId INT NOT NULL,
    ArticleId INT NOT NULL,
    MovementType NVARCHAR(20) NOT NULL,
    Quantity DECIMAL(18,3) NOT NULL,
    ReferenceNumber NVARCHAR(50),
    BatchNumber NVARCHAR(50),
    ExpiryDate DATETIME2,
    Notes NVARCHAR(500),
    CreatedBy NVARCHAR(100),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    FOREIGN KEY (InventoryStockId) REFERENCES InventoryStocks(Id) ON DELETE CASCADE
);";

        private static string CreateInventorySuppliersTable() => @"
CREATE TABLE InventorySuppliers (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(200) NOT NULL,
    Code NVARCHAR(50),
    ContactPerson NVARCHAR(100),
    Phone NVARCHAR(20),
    Email NVARCHAR(100),
    Address NVARCHAR(500),
    PaymentTerms INT NOT NULL DEFAULT 0,
    LeadTimeDays INT NOT NULL DEFAULT 0,
    Rating DECIMAL(3,2) NOT NULL DEFAULT 5.00,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);";

        private static string CreateArticleSuppliersTable() => @"
CREATE TABLE ArticleSuppliers (
    Id INT PRIMARY KEY IDENTITY(1,1),
    ArticleId INT NOT NULL,
    SupplierId INT NOT NULL,
    SupplierProductCode NVARCHAR(50),
    PurchasePrice DECIMAL(18,2) NOT NULL,
    MinOrderQuantity DECIMAL(18,3) NOT NULL,
    IsPreferred BIT NOT NULL DEFAULT 0,
    LeadTimeDays INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    FOREIGN KEY (SupplierId) REFERENCES InventorySuppliers(Id) ON DELETE CASCADE
);";

        private static string CreateReorderSuggestionsTable() => @"
CREATE TABLE ReorderSuggestions (
    Id INT PRIMARY KEY IDENTITY(1,1),
    ArticleId INT NOT NULL,
    SupplierId INT,
    ArticleName NVARCHAR(100) NOT NULL,
    CurrentStock DECIMAL(18,3) NOT NULL,
    MinimumStock DECIMAL(18,3) NOT NULL,
    SuggestedOrderQuantity DECIMAL(18,3) NOT NULL,
    EstimatedCost DECIMAL(18,2) NOT NULL,
    Status NVARCHAR(20) NOT NULL,
    PurchaseOrderId INT,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    FOREIGN KEY (SupplierId) REFERENCES InventorySuppliers(Id)
);";

        private static string CreateStockAlertsTable() => @"
CREATE TABLE StockAlerts (
    Id INT PRIMARY KEY IDENTITY(1,1),
    ArticleId INT NOT NULL,
    ArticleName NVARCHAR(100) NOT NULL,
    AlertType NVARCHAR(20) NOT NULL,
    CurrentQuantity DECIMAL(18,3) NOT NULL,
    MinimumQuantity DECIMAL(18,3) NOT NULL,
    ExpiryDate DATETIME2,
    Status NVARCHAR(20) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    ResolvedAt DATETIME2
);";

        private static string CreateWorkShiftsTable() => @"
CREATE TABLE WorkShifts (
    Id INT PRIMARY KEY IDENTITY(1,1),
    EmployeeId INT NOT NULL,
    EmployeeName NVARCHAR(100) NOT NULL,
    ShiftDate DATE NOT NULL,
    StartTime TIME NOT NULL,
    EndTime TIME NOT NULL,
    ShiftType NVARCHAR(50),
    Status NVARCHAR(20) NOT NULL,
    Notes NVARCHAR(500),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    CreatedBy NVARCHAR(100)
);";

        private static string CreateEmployeePerformancesTable() => @"
CREATE TABLE EmployeePerformances (
    Id INT PRIMARY KEY IDENTITY(1,1),
    EmployeeId INT NOT NULL,
    EmployeeName NVARCHAR(100) NOT NULL,
    Date DATE NOT NULL,
    TotalSales DECIMAL(18,2) NOT NULL DEFAULT 0,
    TransactionCount INT NOT NULL DEFAULT 0,
    AverageTransactionValue DECIMAL(18,2) NOT NULL DEFAULT 0,
    HoursWorked DECIMAL(5,2) NOT NULL DEFAULT 0,
    SalesPerHour DECIMAL(18,2) NOT NULL DEFAULT 0,
    CustomerRating DECIMAL(3,2),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);";

        private static string CreateTimeCardsTable() => @"
CREATE TABLE TimeCards (
    Id INT PRIMARY KEY IDENTITY(1,1),
    EmployeeId INT NOT NULL,
    EmployeeName NVARCHAR(100) NOT NULL,
    ClockIn DATETIME2 NOT NULL,
    ClockOut DATETIME2,
    TotalHours DECIMAL(5,2),
    BreakMinutes INT NOT NULL DEFAULT 0,
    Status NVARCHAR(20) NOT NULL,
    Notes NVARCHAR(500),
    ApprovedBy NVARCHAR(100),
    ApprovedAt DATETIME2,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);";

        private static string CreateLaborCostAnalysesTable() => @"
CREATE TABLE LaborCostAnalyses (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Date DATE NOT NULL,
    TotalHours DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalLabor DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalSales DECIMAL(18,2) NOT NULL DEFAULT 0,
    LaborPercentage DECIMAL(5,2) NOT NULL DEFAULT 0,
    SalesPerLaborHour DECIMAL(18,2) NOT NULL DEFAULT 0,
    EmployeeCount INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);";

        private static string CreateEmployeeAvailabilitiesTable() => @"
CREATE TABLE EmployeeAvailabilities (
    Id INT PRIMARY KEY IDENTITY(1,1),
    EmployeeId INT NOT NULL,
    EmployeeName NVARCHAR(100) NOT NULL,
    DayOfWeek INT NOT NULL,
    StartTime TIME NOT NULL,
    EndTime TIME NOT NULL,
    IsAvailable BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);";

        private static string CreateTimeOffRequestsTable() => @"
CREATE TABLE TimeOffRequests (
    Id INT PRIMARY KEY IDENTITY(1,1),
    EmployeeId INT NOT NULL,
    EmployeeName NVARCHAR(100) NOT NULL,
    RequestType NVARCHAR(50) NOT NULL,
    StartDate DATE NOT NULL,
    EndDate DATE NOT NULL,
    Reason NVARCHAR(500),
    Status NVARCHAR(20) NOT NULL,
    RequestedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    ReviewedBy NVARCHAR(100),
    ReviewedAt DATETIME2,
    Notes NVARCHAR(500)
);";

        private static string CreateStaffMembersTable() => @"
CREATE TABLE StaffMembers (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(100) NOT NULL,
    Position NVARCHAR(50) NOT NULL DEFAULT '',
    Phone NVARCHAR(20) NULL,
    Email NVARCHAR(100) NULL,
    HourlyRate DECIMAL(18,2) NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    HireDate DATETIME2 NOT NULL DEFAULT GETDATE(),
    TerminationDate DATETIME2 NULL,
    Notes NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);";

        private static string CreatePizzaCategoriesTable() => @"
CREATE TABLE PizzaCategories (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NULL,
    Icon NVARCHAR(10) NULL,
    DisplayOrder INT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);";

        private static string CreatePizzaToppingsTable() => @"
CREATE TABLE PizzaToppings (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(100) NOT NULL,
    Category NVARCHAR(50) NULL,
    Price DECIMAL(18,2) NOT NULL DEFAULT 0,
    Icon NVARCHAR(10) NULL,
    DisplayOrder INT NOT NULL DEFAULT 0,
    IsAvailable BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);";

        private static string CreatePOSUsersLegacyTable() => @"
CREATE TABLE POSUsers_Legacy (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(100) NOT NULL,
    PasswordHash NVARCHAR(256) NOT NULL DEFAULT '',
    Email NVARCHAR(100) NULL,
    FullName NVARCHAR(200) NOT NULL DEFAULT '',
    Role NVARCHAR(50) NOT NULL DEFAULT 'Cashier',
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    LastLogin DATETIME2 NULL,
    Branch NVARCHAR(200) NULL,
    PhoneNumber NVARCHAR(50) NULL,
    CanManageArticles BIT NOT NULL DEFAULT 1,
    CanManagePurchases BIT NOT NULL DEFAULT 0,
    CanManageUsers BIT NOT NULL DEFAULT 0,
    CanViewReports BIT NOT NULL DEFAULT 1,
    CanModifyPrices BIT NOT NULL DEFAULT 0,
    CanDeleteReceipts BIT NOT NULL DEFAULT 0,
    CanGiveDiscounts BIT NOT NULL DEFAULT 0,
    MaxDiscountPercent DECIMAL(5,2) NOT NULL DEFAULT 0,
    CanManageDelivery BIT NOT NULL DEFAULT 0,
    CanManageKitchen BIT NOT NULL DEFAULT 0,
    CanManageTables BIT NOT NULL DEFAULT 0,
    CanManageLoyalty BIT NOT NULL DEFAULT 0,
    CanViewAnalytics BIT NOT NULL DEFAULT 1,
    CanManageInventory BIT NOT NULL DEFAULT 0,
    CanAccessStaffScheduling BIT NOT NULL DEFAULT 0
);";

        /// <summary>
        /// Ensures DeliveryOrders.OrderNumber has a database-level default so legacy rows
        /// and EF inserts that omit it do not fail with a NOT NULL violation.
        /// </summary>
        private static async Task EnsureDeliveryOrderNumberDefaultAsync(string connectionString)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Check table exists
                var tableCheck = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DeliveryOrders'";
                using var tableCmd = new SqlCommand(tableCheck, connection);
                if ((int)await tableCmd.ExecuteScalarAsync() == 0) return;

                // Add default constraint if not already present
                var defaultCheck = @"
                    SELECT COUNT(*) FROM sys.default_constraints dc
                    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
                    WHERE dc.parent_object_id = OBJECT_ID('DeliveryOrders') AND c.name = 'OrderNumber'";
                using var defCmd = new SqlCommand(defaultCheck, connection);
                if ((int)await defCmd.ExecuteScalarAsync() == 0)
                {
                    var addDefaultSql = "ALTER TABLE [DeliveryOrders] ADD CONSTRAINT [DF_DeliveryOrders_OrderNumber] DEFAULT ('') FOR [OrderNumber]";
                    using var addCmd = new SqlCommand(addDefaultSql, connection);
                    addCmd.CommandTimeout = 30;
                    await addCmd.ExecuteNonQueryAsync();
                    Console.WriteLine("✓ Added default constraint for DeliveryOrders.OrderNumber");
                }

                // Backfill any existing rows that have an empty OrderNumber
                var backfillSql = @"
                    UPDATE [DeliveryOrders]
                    SET [OrderNumber] = 'DEL-' + FORMAT(CreatedAt, 'yyyyMMdd') + '-' + CAST(Id AS NVARCHAR)
                    WHERE [OrderNumber] = '' OR [OrderNumber] IS NULL";
                using var backfillCmd = new SqlCommand(backfillSql, connection);
                backfillCmd.CommandTimeout = 60;
                await backfillCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error fixing DeliveryOrders.OrderNumber: {ex.Message}");
            }
        }
    }
}
