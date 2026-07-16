using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KosovaPOS.Database.Migrations
{
    /// <summary>
    /// Comprehensive migration for all restaurant management features:
    /// - Inventory Management System
    /// - Customer Loyalty Program
    /// - Staff Scheduling & Time Tracking
    /// - Kitchen Display System (KDS)
    /// </summary>
    public partial class AddRestaurantManagementFeatures : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============ INVENTORY MANAGEMENT TABLES ============
            
            migrationBuilder.CreateTable(
                name: "InventoryStocks",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<int>(nullable: false),
                    ArticleName = table.Column<string>(maxLength: 100, nullable: false),
                    CurrentQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    MinimumQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ReorderQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Unit = table.Column<string>(maxLength: 20, nullable: false),
                    LastRestockedAt = table.Column<DateTime>(nullable: true),
                    Location = table.Column<string>(maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryStocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryMovements",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    InventoryStockId = table.Column<int>(nullable: false),
                    ArticleId = table.Column<int>(nullable: false),
                    MovementType = table.Column<string>(maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ReferenceNumber = table.Column<string>(maxLength: 50, nullable: true),
                    BatchNumber = table.Column<string>(maxLength: 50, nullable: true),
                    ExpiryDate = table.Column<DateTime>(nullable: true),
                    Notes = table.Column<string>(maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_InventoryStocks_InventoryStockId",
                        column: x => x.InventoryStockId,
                        principalTable: "InventoryStocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventorySuppliers",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(maxLength: 200, nullable: false),
                    Code = table.Column<string>(maxLength: 50, nullable: true),
                    ContactPerson = table.Column<string>(maxLength: 100, nullable: true),
                    Phone = table.Column<string>(maxLength: 20, nullable: true),
                    Email = table.Column<string>(maxLength: 100, nullable: true),
                    Address = table.Column<string>(maxLength: 500, nullable: true),
                    PaymentTerms = table.Column<int>(nullable: false),
                    LeadTimeDays = table.Column<int>(nullable: false),
                    Rating = table.Column<decimal>(type: "decimal(3,2)", nullable: false),
                    IsActive = table.Column<bool>(nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventorySuppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArticleSuppliers",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<int>(nullable: false),
                    SupplierId = table.Column<int>(nullable: false),
                    SupplierProductCode = table.Column<string>(maxLength: 50, nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinOrderQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    IsPreferred = table.Column<bool>(nullable: false),
                    LeadTimeDays = table.Column<int>(nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleSuppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleSuppliers_InventorySuppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "InventorySuppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReorderSuggestions",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<int>(nullable: false),
                    SupplierId = table.Column<int>(nullable: true),
                    ArticleName = table.Column<string>(maxLength: 100, nullable: false),
                    CurrentStock = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    MinimumStock = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    SuggestedOrderQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(maxLength: 20, nullable: false),
                    PurchaseOrderId = table.Column<int>(nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReorderSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReorderSuggestions_InventorySuppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "InventorySuppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<int>(nullable: false),
                    ArticleName = table.Column<string>(maxLength: 100, nullable: false),
                    AlertType = table.Column<string>(maxLength: 20, nullable: false),
                    CurrentQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    MinimumQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ExpiryDate = table.Column<DateTime>(nullable: true),
                    Status = table.Column<string>(maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    ResolvedAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAlerts", x => x.Id);
                });

            // ============ CUSTOMER LOYALTY TABLES ============

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(maxLength: 100, nullable: false),
                    Phone = table.Column<string>(maxLength: 20, nullable: true),
                    Email = table.Column<string>(maxLength: 100, nullable: true),
                    LoyaltyCardNumber = table.Column<string>(maxLength: 50, nullable: true),
                    LoyaltyPoints = table.Column<int>(nullable: false),
                    TotalPointsEarned = table.Column<int>(nullable: false),
                    TotalPointsRedeemed = table.Column<int>(nullable: false),
                    Tier = table.Column<string>(maxLength: 20, nullable: false),
                    TotalSpent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OrderCount = table.Column<int>(nullable: false),
                    Birthday = table.Column<DateTime>(nullable: true),
                    PreferredContact = table.Column<string>(maxLength: 20, nullable: true),
                    Address = table.Column<string>(maxLength: 500, nullable: true),
                    City = table.Column<string>(maxLength: 100, nullable: true),
                    Preferences = table.Column<string>(nullable: true),
                    MarketingOptIn = table.Column<bool>(nullable: false),
                    IsActive = table.Column<bool>(nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: false),
                    LastVisit = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(nullable: false),
                    Points = table.Column<int>(nullable: false),
                    TransactionType = table.Column<string>(maxLength: 20, nullable: false),
                    ReferenceNumber = table.Column<string>(maxLength: 50, nullable: true),
                    PurchaseAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Description = table.Column<string>(maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyTransactions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(maxLength: 200, nullable: false),
                    Description = table.Column<string>(maxLength: 1000, nullable: true),
                    CampaignType = table.Column<string>(maxLength: 50, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TargetTier = table.Column<string>(maxLength: 20, nullable: true),
                    MinimumPurchase = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ApplicableArticles = table.Column<string>(nullable: true),
                    StartDate = table.Column<DateTime>(nullable: false),
                    EndDate = table.Column<DateTime>(nullable: false),
                    Status = table.Column<string>(maxLength: 20, nullable: false),
                    UsageCount = table.Column<int>(nullable: false),
                    TotalRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CampaignUsages",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    CampaignId = table.Column<int>(nullable: false),
                    CustomerId = table.Column<int>(nullable: false),
                    ReceiptId = table.Column<int>(nullable: false),
                    BenefitAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UsedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignUsages_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignUsages_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerOrderHistories",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(nullable: false),
                    ReceiptId = table.Column<int>(nullable: false),
                    OrderTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PointsEarned = table.Column<int>(nullable: false),
                    OrderItems = table.Column<string>(nullable: true),
                    OrderDate = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerOrderHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerOrderHistories_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ============ STAFF SCHEDULING TABLES ============

            migrationBuilder.CreateTable(
                name: "WorkShifts",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(nullable: false),
                    EmployeeName = table.Column<string>(maxLength: 100, nullable: false),
                    ShiftType = table.Column<string>(maxLength: 20, nullable: false),
                    ShiftDate = table.Column<DateTime>(nullable: false),
                    StartTime = table.Column<TimeSpan>(nullable: false),
                    EndTime = table.Column<TimeSpan>(nullable: false),
                    ClockInTime = table.Column<DateTime>(nullable: true),
                    ClockOutTime = table.Column<DateTime>(nullable: true),
                    BreakMinutes = table.Column<int>(nullable: false),
                    Status = table.Column<string>(maxLength: 20, nullable: false),
                    Position = table.Column<string>(maxLength: 50, nullable: true),
                    Notes = table.Column<string>(maxLength: 500, nullable: true),
                    HourlyRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HoursWorked = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LaborCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkShifts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeCards",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(nullable: false),
                    EmployeeName = table.Column<string>(maxLength: 100, nullable: false),
                    ClockInTime = table.Column<DateTime>(nullable: false),
                    ClockOutTime = table.Column<DateTime>(nullable: true),
                    BreakStartTime = table.Column<DateTime>(nullable: true),
                    BreakEndTime = table.Column<DateTime>(nullable: true),
                    TotalBreakMinutes = table.Column<int>(nullable: false),
                    TotalHours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ShiftId = table.Column<int>(nullable: true),
                    Notes = table.Column<string>(maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeCards_WorkShifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "WorkShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeePerformances",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(nullable: false),
                    EmployeeName = table.Column<string>(maxLength: 100, nullable: false),
                    PeriodStart = table.Column<DateTime>(nullable: false),
                    PeriodEnd = table.Column<DateTime>(nullable: false),
                    ShiftsWorked = table.Column<int>(nullable: false),
                    TotalHours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LateCount = table.Column<int>(nullable: false),
                    AbsenceCount = table.Column<int>(nullable: false),
                    TotalSales = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OrderCount = table.Column<int>(nullable: false),
                    AverageOrderValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CustomerRating = table.Column<decimal>(type: "decimal(3,2)", nullable: true),
                    DeliveriesCompleted = table.Column<int>(nullable: true),
                    AverageDeliveryTime = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PerformanceScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePerformances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LaborCostAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    PeriodStart = table.Column<DateTime>(nullable: false),
                    PeriodEnd = table.Column<DateTime>(nullable: false),
                    TotalLaborCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LaborCostPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TotalHours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AverageHourlyCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EmployeeCount = table.Column<int>(nullable: false),
                    CostByPosition = table.Column<string>(nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaborCostAnalyses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeAvailabilities",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(nullable: false),
                    EmployeeName = table.Column<string>(maxLength: 100, nullable: false),
                    DayOfWeek = table.Column<string>(maxLength: 20, nullable: false),
                    StartTime = table.Column<TimeSpan>(nullable: false),
                    EndTime = table.Column<TimeSpan>(nullable: false),
                    IsAvailable = table.Column<bool>(nullable: false),
                    PreferredShift = table.Column<string>(maxLength: 20, nullable: true),
                    MaxHoursPerWeek = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeAvailabilities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeOffRequests",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(nullable: false),
                    EmployeeName = table.Column<string>(maxLength: 100, nullable: false),
                    RequestType = table.Column<string>(maxLength: 20, nullable: false),
                    StartDate = table.Column<DateTime>(nullable: false),
                    EndDate = table.Column<DateTime>(nullable: false),
                    Reason = table.Column<string>(maxLength: 1000, nullable: true),
                    Status = table.Column<string>(maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<string>(maxLength: 100, nullable: true),
                    ApprovedAt = table.Column<DateTime>(nullable: true),
                    ApprovalNotes = table.Column<string>(maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeOffRequests", x => x.Id);
                });

            // ============ KITCHEN DISPLAY SYSTEM TABLES ============

            migrationBuilder.CreateTable(
                name: "KitchenStations",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(maxLength: 100, nullable: false),
                    Code = table.Column<string>(maxLength: 20, nullable: false),
                    Description = table.Column<string>(maxLength: 500, nullable: true),
                    HandledArticles = table.Column<string>(nullable: true),
                    AveragePrepTime = table.Column<int>(nullable: false),
                    MaxConcurrentOrders = table.Column<int>(nullable: false),
                    DisplayColor = table.Column<string>(maxLength: 20, nullable: true),
                    DisplayOrder = table.Column<int>(nullable: false),
                    IsActive = table.Column<bool>(nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitchenStations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KitchenOrders",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    ReceiptId = table.Column<int>(nullable: false),
                    OrderNumber = table.Column<int>(nullable: false),
                    OrderType = table.Column<string>(maxLength: 20, nullable: false),
                    TableNumber = table.Column<string>(maxLength: 20, nullable: true),
                    CustomerName = table.Column<string>(maxLength: 100, nullable: true),
                    OrderItems = table.Column<string>(nullable: true),
                    SpecialInstructions = table.Column<string>(maxLength: 1000, nullable: true),
                    Status = table.Column<string>(maxLength: 20, nullable: false),
                    Priority = table.Column<string>(maxLength: 20, nullable: false),
                    Station = table.Column<string>(maxLength: 50, nullable: true),
                    AssignedTo = table.Column<string>(maxLength: 100, nullable: true),
                    ReceivedAt = table.Column<DateTime>(nullable: false),
                    StartedAt = table.Column<DateTime>(nullable: true),
                    ReadyAt = table.Column<DateTime>(nullable: true),
                    ServedAt = table.Column<DateTime>(nullable: true),
                    TargetTime = table.Column<int>(nullable: false),
                    ActualTime = table.Column<int>(nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitchenOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KitchenOrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    KitchenOrderId = table.Column<int>(nullable: false),
                    ArticleId = table.Column<int>(nullable: false),
                    ItemName = table.Column<string>(maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(nullable: false),
                    Status = table.Column<string>(maxLength: 20, nullable: false),
                    Modifications = table.Column<string>(nullable: true),
                    SpecialInstructions = table.Column<string>(maxLength: 500, nullable: true),
                    Station = table.Column<string>(maxLength: 50, nullable: true),
                    SequenceNumber = table.Column<int>(nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitchenOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KitchenOrderItems_KitchenOrders_KitchenOrderId",
                        column: x => x.KitchenOrderId,
                        principalTable: "KitchenOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientPreps",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    IngredientId = table.Column<int>(nullable: false),
                    IngredientName = table.Column<string>(maxLength: 100, nullable: false),
                    PrepType = table.Column<string>(maxLength: 50, nullable: false),
                    QuantityPrepared = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Unit = table.Column<string>(maxLength: 20, nullable: false),
                    CurrentQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    MinimumQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(nullable: true),
                    PreparedBy = table.Column<string>(maxLength: 100, nullable: true),
                    PreparedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientPreps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KitchenPerformances",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    PeriodStart = table.Column<DateTime>(nullable: false),
                    PeriodEnd = table.Column<DateTime>(nullable: false),
                    Station = table.Column<string>(maxLength: 100, nullable: true),
                    TotalOrders = table.Column<int>(nullable: false),
                    OrdersOnTime = table.Column<int>(nullable: false),
                    OrdersLate = table.Column<int>(nullable: false),
                    AveragePrepTime = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PeakPrepTime = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FastestPrepTime = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OrdersCancelled = table.Column<int>(nullable: false),
                    OnTimePercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitchenPerformances", x => x.Id);
                });

            // Create indexes for better performance
            migrationBuilder.CreateIndex(name: "IX_InventoryStocks_ArticleId", table: "InventoryStocks", column: "ArticleId");
            migrationBuilder.CreateIndex(name: "IX_InventoryMovements_InventoryStockId", table: "InventoryMovements", column: "InventoryStockId");
            migrationBuilder.CreateIndex(name: "IX_ArticleSuppliers_SupplierId", table: "ArticleSuppliers", column: "SupplierId");
            migrationBuilder.CreateIndex(name: "IX_ArticleSuppliers_ArticleId", table: "ArticleSuppliers", column: "ArticleId");
            migrationBuilder.CreateIndex(name: "IX_Customers_Phone", table: "Customers", column: "Phone");
            migrationBuilder.CreateIndex(name: "IX_Customers_Email", table: "Customers", column: "Email");
            migrationBuilder.CreateIndex(name: "IX_Customers_LoyaltyCardNumber", table: "Customers", column: "LoyaltyCardNumber", unique: true, filter: "[LoyaltyCardNumber] IS NOT NULL");
            migrationBuilder.CreateIndex(name: "IX_LoyaltyTransactions_CustomerId", table: "LoyaltyTransactions", column: "CustomerId");
            migrationBuilder.CreateIndex(name: "IX_CampaignUsages_CampaignId", table: "CampaignUsages", column: "CampaignId");
            migrationBuilder.CreateIndex(name: "IX_CampaignUsages_CustomerId", table: "CampaignUsages", column: "CustomerId");
            migrationBuilder.CreateIndex(name: "IX_CustomerOrderHistories_CustomerId", table: "CustomerOrderHistories", column: "CustomerId");
            migrationBuilder.CreateIndex(name: "IX_WorkShifts_EmployeeId", table: "WorkShifts", column: "EmployeeId");
            migrationBuilder.CreateIndex(name: "IX_WorkShifts_ShiftDate", table: "WorkShifts", column: "ShiftDate");
            migrationBuilder.CreateIndex(name: "IX_TimeCards_EmployeeId", table: "TimeCards", column: "EmployeeId");
            migrationBuilder.CreateIndex(name: "IX_TimeCards_ShiftId", table: "TimeCards", column: "ShiftId");
            migrationBuilder.CreateIndex(name: "IX_KitchenOrders_ReceiptId", table: "KitchenOrders", column: "ReceiptId");
            migrationBuilder.CreateIndex(name: "IX_KitchenOrders_Status", table: "KitchenOrders", column: "Status");
            migrationBuilder.CreateIndex(name: "IX_KitchenOrderItems_KitchenOrderId", table: "KitchenOrderItems", column: "KitchenOrderId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop all tables in reverse order
            migrationBuilder.DropTable(name: "KitchenPerformances");
            migrationBuilder.DropTable(name: "IngredientPreps");
            migrationBuilder.DropTable(name: "KitchenOrderItems");
            migrationBuilder.DropTable(name: "KitchenOrders");
            migrationBuilder.DropTable(name: "KitchenStations");
            migrationBuilder.DropTable(name: "TimeOffRequests");
            migrationBuilder.DropTable(name: "EmployeeAvailabilities");
            migrationBuilder.DropTable(name: "LaborCostAnalyses");
            migrationBuilder.DropTable(name: "EmployeePerformances");
            migrationBuilder.DropTable(name: "TimeCards");
            migrationBuilder.DropTable(name: "WorkShifts");
            migrationBuilder.DropTable(name: "CustomerOrderHistories");
            migrationBuilder.DropTable(name: "CampaignUsages");
            migrationBuilder.DropTable(name: "Campaigns");
            migrationBuilder.DropTable(name: "LoyaltyTransactions");
            migrationBuilder.DropTable(name: "Customers");
            migrationBuilder.DropTable(name: "StockAlerts");
            migrationBuilder.DropTable(name: "ReorderSuggestions");
            migrationBuilder.DropTable(name: "ArticleSuppliers");
            migrationBuilder.DropTable(name: "InventorySuppliers");
            migrationBuilder.DropTable(name: "InventoryMovements");
            migrationBuilder.DropTable(name: "InventoryStocks");
        }
    }
}
