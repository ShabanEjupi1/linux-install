using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KosovaPOS.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Threshold = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SendEmail = table.Column<bool>(type: "boolean", nullable: false),
                    EmailRecipient = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LastTriggered = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApiRequestLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    RemoteIp = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiRequestLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EmployeeId = table.Column<int>(type: "integer", nullable: true),
                    EmployeeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ServiceArticleId = table.Column<int>(type: "integer", nullable: true),
                    ServiceName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ServicePrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Deposit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReminderSent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReceiptId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArkaDalje",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Pershkrimi = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Punetori = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Data = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Vlera = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArkaDalje", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ArkaHyrje",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Pershkrimi = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Punetori = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Data = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Vlera = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArkaHyrje", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ArkaHyrjeDalje",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Pershkrimi = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DOK = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Subjekti = table.Column<int>(type: "integer", nullable: true),
                    Data = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MetodaP = table.Column<int>(type: "integer", nullable: true),
                    VleraH = table.Column<double>(type: "double precision", nullable: true),
                    VleraD = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArkaHyrjeDalje", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Arkat",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kodi = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Sektori = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Emri = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Pershkrimi = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Arkat", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SalesUnit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Pack = table.Column<decimal>(type: "numeric", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "numeric", nullable: false),
                    Margin = table.Column<decimal>(type: "numeric", nullable: false),
                    PackagePrice = table.Column<decimal>(type: "numeric", nullable: false),
                    WholesalePrice = table.Column<decimal>(type: "numeric", nullable: false),
                    SalesPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    SalesPrice1 = table.Column<decimal>(type: "numeric", nullable: false),
                    VATRate = table.Column<decimal>(type: "numeric", nullable: false),
                    VATType = table.Column<int>(type: "integer", nullable: false),
                    PLU = table.Column<int>(type: "integer", nullable: true),
                    Category = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Supplier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    Size = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Importer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Sector = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Branch = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Season = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StockQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    StockIn = table.Column<decimal>(type: "numeric", nullable: false),
                    StockOut = table.Column<decimal>(type: "numeric", nullable: false),
                    AverageSalesPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    AveragePurchasePrice = table.Column<decimal>(type: "numeric", nullable: false),
                    MinimumStock = table.Column<decimal>(type: "numeric", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    HasBarcode = table.Column<bool>(type: "boolean", nullable: false),
                    IsRegular = table.Column<bool>(type: "boolean", nullable: false),
                    IsWeighed = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ProductType = table.Column<int>(type: "integer", nullable: false),
                    POSCategoryId = table.Column<int>(type: "integer", nullable: true),
                    PhotoPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArticleVariants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParentArticleId = table.Column<int>(type: "integer", nullable: false),
                    ParentBarcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ParentName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Size = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Material = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StockQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    SalesPriceOverride = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    PLU = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleVariants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Artikujt",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Barkodi = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Emertimi = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    NjesiaP = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NjesiaSH = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Kategoria = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    PaBarkod = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    IRregullt = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    CFurnizimit = table.Column<double>(type: "double precision", nullable: true),
                    Marzha = table.Column<double>(type: "double precision", nullable: true),
                    Paketimi = table.Column<double>(type: "double precision", nullable: true),
                    CPaketimit = table.Column<double>(type: "double precision", nullable: true),
                    CShumices = table.Column<double>(type: "double precision", nullable: true),
                    CShitjes = table.Column<double>(type: "double precision", nullable: true),
                    CShitjes1 = table.Column<double>(type: "double precision", nullable: true),
                    Sasia = table.Column<double>(type: "double precision", nullable: true),
                    Afati = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Furnitori = table.Column<int>(type: "integer", nullable: true),
                    Verejtje = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Filiala = table.Column<int>(type: "integer", nullable: true),
                    Sektori = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SasiaHyrje = table.Column<double>(type: "double precision", nullable: true),
                    SasiaDalje = table.Column<double>(type: "double precision", nullable: true),
                    CMesatarShites = table.Column<double>(type: "double precision", nullable: true),
                    CMesatarFurnizues = table.Column<double>(type: "double precision", nullable: true),
                    Vendi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Prodhuesi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Importuesi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tatiminr = table.Column<double>(type: "double precision", nullable: true),
                    Tatimi = table.Column<double>(type: "double precision", nullable: true),
                    Shpenzim = table.Column<bool>(type: "boolean", nullable: true),
                    Peshore = table.Column<bool>(type: "boolean", nullable: true),
                    Vat = table.Column<int>(type: "integer", nullable: true),
                    Tipi = table.Column<int>(type: "integer", nullable: true),
                    Foto = table.Column<byte[]>(type: "bytea", nullable: true),
                    PhotoPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    KategoriaPos_ID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artikujt", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ATK_CorporateTaxDeclarations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    TotalRevenue = table.Column<decimal>(type: "numeric", nullable: false),
                    AllowableDeductions = table.Column<decimal>(type: "numeric", nullable: false),
                    TaxableIncome = table.Column<decimal>(type: "numeric", nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric", nullable: false),
                    TaxLiability = table.Column<decimal>(type: "numeric", nullable: false),
                    PrepaymentsMade = table.Column<decimal>(type: "numeric", nullable: false),
                    TaxPayable = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FiledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATK_CorporateTaxDeclarations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ATK_PayrollTaxDeclarations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    EmployeeCount = table.Column<int>(type: "integer", nullable: false),
                    TotalGrossWages = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployeePension = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployerPension = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalTAP = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalContributions = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FiledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATK_PayrollTaxDeclarations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ATK_TVShDeclarations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    SalesStandardRate = table.Column<decimal>(type: "numeric", nullable: false),
                    SalesReducedRate = table.Column<decimal>(type: "numeric", nullable: false),
                    SalesZeroRate = table.Column<decimal>(type: "numeric", nullable: false),
                    SalesExempt = table.Column<decimal>(type: "numeric", nullable: false),
                    OutputVAT = table.Column<decimal>(type: "numeric", nullable: false),
                    PurchasesStandard = table.Column<decimal>(type: "numeric", nullable: false),
                    PurchasesReduced = table.Column<decimal>(type: "numeric", nullable: false),
                    InputVAT = table.Column<decimal>(type: "numeric", nullable: false),
                    NetVAT = table.Column<decimal>(type: "numeric", nullable: false),
                    VATPayable = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FiledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATK_TVShDeclarations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ATK_WithholdingTaxDeclarations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    PayeeBusinessName = table.Column<string>(type: "text", nullable: true),
                    PayeeNUI = table.Column<string>(type: "text", nullable: true),
                    ServiceDescription = table.Column<string>(type: "text", nullable: true),
                    GrossPayment = table.Column<decimal>(type: "numeric", nullable: false),
                    WithholdingRate = table.Column<decimal>(type: "numeric", nullable: false),
                    TaxWithheld = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATK_WithholdingTaxDeclarations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<int>(type: "integer", nullable: true),
                    EntityName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Details = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Borxhi",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Klienti = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Data = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Vlera = table.Column<double>(type: "double precision", nullable: true),
                    Pagoi = table.Column<double>(type: "double precision", nullable: true),
                    Mbeti = table.Column<double>(type: "double precision", nullable: true),
                    Pershkrimi = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Borxhi", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NRF = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NUI = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PartnerType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Balance = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPartners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Profile = table.Column<int>(type: "integer", nullable: false),
                    IsFirstRun = table.Column<bool>(type: "boolean", nullable: false),
                    LogoPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BusinessName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FiscalNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EnableTableManagement = table.Column<bool>(type: "boolean", nullable: false),
                    EnableAppointments = table.Column<bool>(type: "boolean", nullable: false),
                    EnableRentals = table.Column<bool>(type: "boolean", nullable: false),
                    EnableKitchenDisplay = table.Column<bool>(type: "boolean", nullable: false),
                    EnableLoyalty = table.Column<bool>(type: "boolean", nullable: false),
                    EnableDelivery = table.Column<bool>(type: "boolean", nullable: false),
                    EnableProductionBOM = table.Column<bool>(type: "boolean", nullable: false),
                    EnableMultiCurrency = table.Column<bool>(type: "boolean", nullable: false),
                    EnableShiftManagement = table.Column<bool>(type: "boolean", nullable: false),
                    EnableVariants = table.Column<bool>(type: "boolean", nullable: false),
                    EnablePriceRules = table.Column<bool>(type: "boolean", nullable: false),
                    EnablePurchaseOrders = table.Column<bool>(type: "boolean", nullable: false),
                    EnableGiftCards = table.Column<bool>(type: "boolean", nullable: false),
                    EnableBundles = table.Column<bool>(type: "boolean", nullable: false),
                    EnableNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    EnableWeighingScale = table.Column<bool>(type: "boolean", nullable: false),
                    EnableCustomerDisplay = table.Column<bool>(type: "boolean", nullable: false),
                    EnableCloudBackup = table.Column<bool>(type: "boolean", nullable: false),
                    EnableOfflineQueue = table.Column<bool>(type: "boolean", nullable: false),
                    EnableRestApi = table.Column<bool>(type: "boolean", nullable: false),
                    RestApiPort = table.Column<int>(type: "integer", nullable: false),
                    RestApiKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    WebhookUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BackupScheduleHour = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashShifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OpenedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    OpeningCash = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ClosingCash = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ExpectedCash = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CashDifference = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CashierId = table.Column<int>(type: "integer", nullable: true),
                    CashierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalSales = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalCashSales = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalCardSales = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalDiscounts = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TransactionCount = table.Column<int>(type: "integer", nullable: false),
                    ItemsSold = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DenominationCountJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashShifts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    IsBase = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReceiptId = table.Column<int>(type: "integer", nullable: true),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DeliveryAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CustomerPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DriverId = table.Column<int>(type: "integer", nullable: true),
                    DriverName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OrderTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DeliveryFee = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    EstimatedDelivery = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ActualDelivery = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DeliveryNotePrinted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DitariD",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DATA = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ORA = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    NUMRI = table.Column<long>(type: "bigint", nullable: true),
                    KUPONI = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ARKA = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SEKTORI = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SUBJEKTI = table.Column<int>(type: "integer", nullable: true),
                    PUNETORI = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MUAJI = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    VITI = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    VEREJTJE = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BARKODI = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ARTIKULLI = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    NJESIA = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SASIA = table.Column<double>(type: "double precision", nullable: true),
                    QMIMI = table.Column<double>(type: "double precision", nullable: true),
                    QMIMI1 = table.Column<double>(type: "double precision", nullable: true),
                    QMIMIPATVSH = table.Column<double>(type: "double precision", nullable: true),
                    QMIMIPATVSH1 = table.Column<double>(type: "double precision", nullable: true),
                    QMIMIF = table.Column<double>(type: "double precision", nullable: true),
                    RABATI = table.Column<double>(type: "double precision", nullable: true),
                    VLERARABATIT = table.Column<double>(type: "double precision", nullable: true),
                    VLERARABATIT1 = table.Column<double>(type: "double precision", nullable: true),
                    TVSH = table.Column<double>(type: "double precision", nullable: true),
                    TVSH1 = table.Column<double>(type: "double precision", nullable: true),
                    VLERAPATVSH = table.Column<double>(type: "double precision", nullable: true),
                    VLERAPATVSH1 = table.Column<double>(type: "double precision", nullable: true),
                    VLERAMETVSH = table.Column<double>(type: "double precision", nullable: true),
                    VLERAMETVSH1 = table.Column<double>(type: "double precision", nullable: true),
                    BMD = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PAKETIMI = table.Column<double>(type: "double precision", nullable: true),
                    QMIMISHUMICES = table.Column<double>(type: "double precision", nullable: true),
                    FILIALA = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PERPUNIMI = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    KATEGORIA = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    NrFiskalKlient = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AdresaKlient = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ShifraKlient = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PAGOI = table.Column<double>(type: "double precision", nullable: true),
                    MBETI = table.Column<double>(type: "double precision", nullable: true),
                    chk = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    MUAJINR = table.Column<long>(type: "bigint", nullable: true),
                    VAT = table.Column<double>(type: "double precision", nullable: true),
                    MetodaP = table.Column<int>(type: "integer", nullable: true),
                    Banka = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tatimi = table.Column<double>(type: "double precision", nullable: true),
                    tatiminr = table.Column<double>(type: "double precision", nullable: true),
                    Artikulli_ID = table.Column<int>(type: "integer", nullable: true),
                    Nr_Rendor = table.Column<int>(type: "integer", nullable: true),
                    TavolinaID = table.Column<int>(type: "integer", nullable: true),
                    StatusiRestorant = table.Column<bool>(type: "boolean", nullable: true),
                    Pershkrimi = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Vetura = table.Column<int>(type: "integer", nullable: true),
                    Parcela = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DitariD", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "DitariH",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DATA = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ORA = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    NUMRI = table.Column<long>(type: "bigint", nullable: true),
                    KUPONI = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NrFatures = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NrDUD = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FILIALA = table.Column<int>(type: "integer", nullable: true),
                    SUBJEKTI = table.Column<int>(type: "integer", nullable: true),
                    PUNETORI = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TIPI = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BARKODI = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ARTIKULLI = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    NJESIA = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Sasia = table.Column<double>(type: "double precision", nullable: true),
                    Cmimi_Furn = table.Column<double>(type: "double precision", nullable: true),
                    Rabati_Per = table.Column<double>(type: "double precision", nullable: true),
                    Rabati_Vl = table.Column<double>(type: "double precision", nullable: true),
                    Vlera_Furn = table.Column<double>(type: "double precision", nullable: true),
                    Transporti_Vl = table.Column<double>(type: "double precision", nullable: true),
                    Shpenzimet_Vl = table.Column<double>(type: "double precision", nullable: true),
                    Baza_Per_Dogane = table.Column<double>(type: "double precision", nullable: true),
                    Dogana_Per = table.Column<double>(type: "double precision", nullable: true),
                    Dogana_Vl = table.Column<double>(type: "double precision", nullable: true),
                    Aksiza_Vl = table.Column<double>(type: "double precision", nullable: true),
                    Tvsh_Vl = table.Column<double>(type: "double precision", nullable: true),
                    Tvsh_Per = table.Column<double>(type: "double precision", nullable: true),
                    Cmimi_Kushtues = table.Column<double>(type: "double precision", nullable: true),
                    Vlera_Kushtuese = table.Column<double>(type: "double precision", nullable: true),
                    Marzha_Per = table.Column<double>(type: "double precision", nullable: true),
                    Cm_Shitjes = table.Column<double>(type: "double precision", nullable: true),
                    Cm_Shitjes_1 = table.Column<double>(type: "double precision", nullable: true),
                    Vlera_Me_Tvsh = table.Column<double>(type: "double precision", nullable: true),
                    Perpunimi = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    VEREJTJE = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PerPagese = table.Column<double>(type: "double precision", nullable: true),
                    Pagoi = table.Column<double>(type: "double precision", nullable: true),
                    Mbeti = table.Column<double>(type: "double precision", nullable: true),
                    TextMetoda = table.Column<int>(type: "integer", nullable: true),
                    Artikulli_ID = table.Column<int>(type: "integer", nullable: true),
                    Nr_Rendor = table.Column<int>(type: "integer", nullable: true),
                    Cm_Me_TVSH = table.Column<double>(type: "double precision", nullable: true),
                    TVSH_Jo_Zbritshme = table.Column<bool>(type: "boolean", nullable: true),
                    Tarifa = table.Column<int>(type: "integer", nullable: true),
                    Lloji = table.Column<int>(type: "integer", nullable: true),
                    Vl_Pas_Doganes = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DitariH", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    IsWorkingDay = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Filiala",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kompania = table.Column<int>(type: "integer", nullable: true),
                    Emri = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Vendi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Menaxheri = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Data = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Pershkrimi = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Filiala", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "FurnitoriNew",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Emri = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NRF = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NIT = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Personi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Adresa = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Qyteti = table.Column<int>(type: "integer", nullable: true),
                    Telefoni = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Xhirollogaria = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    F = table.Column<bool>(type: "boolean", nullable: true),
                    K = table.Column<bool>(type: "boolean", nullable: true),
                    Data = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Prejashtuar_TVSH = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FurnitoriNew", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GiftCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InitialBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RemainingBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IssuedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IssuedReceiptId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftCards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GiftCardTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GiftCardId = table.Column<int>(type: "integer", nullable: false),
                    TransactionType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BalanceBefore = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ReceiptId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftCardTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_Employees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PersonalIdNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Nationality = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Position = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Branch = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HireDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TerminationDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ContractType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ContractEndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ProbationPeriod = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WeeklyHours = table.Column<int>(type: "integer", nullable: false),
                    GrossSalary = table.Column<decimal>(type: "numeric", nullable: false),
                    BankAccount = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BankName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AnnualLeaveDays = table.Column<int>(type: "integer", nullable: false),
                    UsedLeaveDays = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EmploymentStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TerminationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_Employees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kategoria",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Emri = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Pershkrimi = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kategoria", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "KategoriaPos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Emri = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Pershkrimi = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KategoriaPos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "KitchenOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReceiptId = table.Column<int>(type: "integer", nullable: true),
                    ReceiptNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TableNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReadyAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ServedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    WaiterName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitchenOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kompania",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kodi = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NF = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NIT = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Tipi = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Emri = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Vendi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Adresa = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Telefoni = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Pronari = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Xhirollogaria = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Tvsh = table.Column<double>(type: "double precision", nullable: true),
                    Sasia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kompania", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "LlojiShpenzimeve",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Emri = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Pershkrimi = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlojiShpenzimeve", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CardNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    TotalSpent = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TierLevel = table.Column<int>(type: "integer", nullable: false),
                    JoinDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PointsPerEuro = table.Column<decimal>(type: "numeric(10,4)", nullable: false),
                    EuroPerPoint = table.Column<decimal>(type: "numeric(10,4)", nullable: false),
                    BronzeThreshold = table.Column<int>(type: "integer", nullable: false),
                    SilverThreshold = table.Column<int>(type: "integer", nullable: false),
                    GoldThreshold = table.Column<int>(type: "integer", nullable: false),
                    PlatinumThreshold = table.Column<int>(type: "integer", nullable: false),
                    MaxRedeemPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoyaltyAccountId = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ReceiptId = table.Column<int>(type: "integer", nullable: true),
                    ReceiptNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PointsMonetaryValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetodaPagese",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Emri = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Numri = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetodaPagese", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "NjesitMatese",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Emri = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Pershkrimi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NjesitMatese", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ActionWindow = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OfflineQueueLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TempReceiptId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SqlReceiptId = table.Column<int>(type: "integer", nullable: true),
                    ReceiptJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    QueuedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfflineQueueLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "POSUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Branch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CanManageArticles = table.Column<bool>(type: "boolean", nullable: false),
                    CanManagePurchases = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageUsers = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewReports = table.Column<bool>(type: "boolean", nullable: false),
                    CanModifyPrices = table.Column<bool>(type: "boolean", nullable: false),
                    CanDeleteReceipts = table.Column<bool>(type: "boolean", nullable: false),
                    CanGiveDiscounts = table.Column<bool>(type: "boolean", nullable: false),
                    MaxDiscountPercent = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POSUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "POSUsers_Legacy",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Branch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CanManageArticles = table.Column<bool>(type: "boolean", nullable: false),
                    CanManagePurchases = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageUsers = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewReports = table.Column<bool>(type: "boolean", nullable: false),
                    CanModifyPrices = table.Column<bool>(type: "boolean", nullable: false),
                    CanDeleteReceipts = table.Column<bool>(type: "boolean", nullable: false),
                    CanGiveDiscounts = table.Column<bool>(type: "boolean", nullable: false),
                    MaxDiscountPercent = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POSUsers_Legacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ArticleId = table.Column<int>(type: "integer", nullable: true),
                    ArticleBarcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CategoryName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    DiscountPercent = table.Column<decimal>(type: "numeric(8,4)", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    MinQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    BuyQuantity = table.Column<int>(type: "integer", nullable: true),
                    GetQuantity = table.Column<int>(type: "integer", nullable: true),
                    DayOfWeekFilter = table.Column<int>(type: "integer", nullable: false),
                    TimeRangeStart = table.Column<TimeSpan>(type: "interval", nullable: true),
                    TimeRangeEnd = table.Column<TimeSpan>(type: "interval", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsStackable = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductBundles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParentArticleId = table.Column<int>(type: "integer", nullable: false),
                    ParentBarcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ParentName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    ComponentArticleId = table.Column<int>(type: "integer", nullable: false),
                    ComponentBarcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ComponentName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    AutoExpandOnSale = table.Column<bool>(type: "boolean", nullable: false),
                    DeductComponentStock = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBundles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Punetoret",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmriMbiemri = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Filiala = table.Column<int>(type: "integer", nullable: true),
                    Shifra = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Niveli = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Data = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Pershkrimi = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Punetoret", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SupplierContact = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OrderDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpectedDelivery = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReceivedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VATAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Qytetet",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Emertimi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Qytetet", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Receipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReceiptNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    BuyerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BuyerBusinessName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BuyerNUI = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BuyerFiscalNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BuyerAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BuyerEmail = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    BuyerPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CashierNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CashierName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    LeftAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReceiptType = table.Column<int>(type: "integer", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    IsFiscal = table.Column<bool>(type: "boolean", nullable: false),
                    IsPrinted = table.Column<bool>(type: "boolean", nullable: false),
                    FiscalFilePath = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RentalAgreements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AgreementNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RentalItemId = table.Column<int>(type: "integer", nullable: false),
                    RentalItemName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpectedReturnDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ActualReturnDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DailyRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    HourlyRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Deposit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalCharged = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DepositReturned = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReceiptId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalAgreements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RentalItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ArticleId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Condition = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DailyRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    HourlyRate = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Deposit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RestaurantTables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TableNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Section = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ActiveReceiptId = table.Column<int>(type: "integer", nullable: true),
                    AssignedWaiterId = table.Column<int>(type: "integer", nullable: true),
                    OccupiedSince = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PositionX = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    PositionY = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    IsRound = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantTables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReturnReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReturnNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    OriginalReceiptNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OriginalReceiptId = table.Column<int>(type: "integer", nullable: true),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RefundMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProcessedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsFiscalStornoPrinted = table.Column<bool>(type: "boolean", nullable: false),
                    FiscalStornoFilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesBookEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BuyerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BuyerNUI = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BuyerFiscalNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BaseVAT18 = table.Column<decimal>(type: "numeric", nullable: false),
                    AmountVAT18 = table.Column<decimal>(type: "numeric", nullable: false),
                    BaseVAT8 = table.Column<decimal>(type: "numeric", nullable: false),
                    AmountVAT8 = table.Column<decimal>(type: "numeric", nullable: false),
                    BaseVAT0 = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalWithoutVAT = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalVAT = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalWithVAT = table.Column<decimal>(type: "numeric", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsFiscal = table.Column<bool>(type: "boolean", nullable: false),
                    CashierName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PeriodKey = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesBookEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sektori",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kodi = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Filiala = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Emri = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Vendi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Menaxheri = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Data = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Pershkrimi = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sektori", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "TableSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ColorHex = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TableSections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tatimi",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kodi = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Emri = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Vlera = table.Column<double>(type: "double precision", nullable: true),
                    TVSH_Jo_Zbritshme = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tatimi", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ZReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReportDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    GeneratedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TotalSalesWithVAT = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalSalesWithoutVAT = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalVAT = table.Column<decimal>(type: "numeric", nullable: false),
                    BaseVAT18 = table.Column<decimal>(type: "numeric", nullable: false),
                    AmountVAT18 = table.Column<decimal>(type: "numeric", nullable: false),
                    BaseVAT8 = table.Column<decimal>(type: "numeric", nullable: false),
                    AmountVAT8 = table.Column<decimal>(type: "numeric", nullable: false),
                    BaseVAT0 = table.Column<decimal>(type: "numeric", nullable: false),
                    CashAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    CardAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    OtherAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalTransactions = table.Column<int>(type: "integer", nullable: false),
                    FiscalTransactions = table.Column<int>(type: "integer", nullable: false),
                    NonFiscalTransactions = table.Column<int>(type: "integer", nullable: false),
                    FiscalDeviceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FiscalZNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsSubmittedToATK = table.Column<bool>(type: "boolean", nullable: false),
                    SubmittedToATKAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Purchases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    PurchaseType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    VATAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Purchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Purchases_BusinessPartners_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HR_EmployeeCertificates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    CertificateType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CertificateNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IssueDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IssuedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IssuedByTitle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_EmployeeCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HR_EmployeeCertificates_HR_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HR_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HR_EmploymentContracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    ContractNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ContractType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Position = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Duties = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WorkPlace = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WeeklyHours = table.Column<int>(type: "integer", nullable: false),
                    HasProbation = table.Column<bool>(type: "boolean", nullable: false),
                    ProbationMonths = table.Column<int>(type: "integer", nullable: false),
                    GrossSalary = table.Column<decimal>(type: "numeric", nullable: false),
                    BonusDescription = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Benefits = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AnnualLeaveDays = table.Column<int>(type: "integer", nullable: false),
                    NoticePeriod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EmployerRepresentative = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmployerRepresentativeTitle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_EmploymentContracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HR_EmploymentContracts_HR_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HR_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HR_LeaveRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    LeaveType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RequestDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_LeaveRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HR_LeaveRequests_HR_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HR_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HR_PayrollRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    GrossSalary = table.Column<decimal>(type: "numeric", nullable: false),
                    Bonus = table.Column<decimal>(type: "numeric", nullable: false),
                    OvertimePay = table.Column<decimal>(type: "numeric", nullable: false),
                    OtherAllowances = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployeePension = table.Column<decimal>(type: "numeric", nullable: false),
                    IncomeTax = table.Column<decimal>(type: "numeric", nullable: false),
                    OtherDeductions = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployerPension = table.Column<decimal>(type: "numeric", nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    PaidDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PayrollRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HR_PayrollRecords_HR_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HR_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PurchaseOrderId = table.Column<int>(type: "integer", nullable: false),
                    ArticleId = table.Column<int>(type: "integer", nullable: true),
                    Barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ArticleName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OrderedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReceivedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VATRate = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderItems_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReceiptId = table.Column<int>(type: "integer", nullable: false),
                    ArticleId = table.Column<int>(type: "integer", nullable: false),
                    PLU = table.Column<int>(type: "integer", nullable: false),
                    Barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ArticleName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "numeric", nullable: false),
                    VATRate = table.Column<decimal>(type: "numeric", nullable: false),
                    VATValue = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalValue = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptItems_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReceiptItems_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReturnReceiptItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReturnReceiptId = table.Column<int>(type: "integer", nullable: false),
                    Barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ArticleName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VATRate = table.Column<decimal>(type: "numeric", nullable: false),
                    ArticleId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnReceiptItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnReceiptItems_ReturnReceipts_ReturnReceiptId",
                        column: x => x.ReturnReceiptId,
                        principalTable: "ReturnReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PurchaseId = table.Column<int>(type: "integer", nullable: false),
                    ArticleId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "numeric", nullable: false),
                    VATRate = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalValue = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseItems_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseItems_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HR_EmployeeCertificates_EmployeeId",
                table: "HR_EmployeeCertificates",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_EmploymentContracts_EmployeeId",
                table: "HR_EmploymentContracts",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_LeaveRequests_EmployeeId",
                table: "HR_LeaveRequests",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollRecords_EmployeeId",
                table: "HR_PayrollRecords",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseItems_ArticleId",
                table: "PurchaseItems",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseItems_PurchaseId",
                table: "PurchaseItems",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_PurchaseOrderId",
                table: "PurchaseOrderItems",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_SupplierId",
                table: "Purchases",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptItems_ArticleId",
                table: "ReceiptItems",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptItems_ReceiptId",
                table: "ReceiptItems",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnReceiptItems_ReturnReceiptId",
                table: "ReturnReceiptItems",
                column: "ReturnReceiptId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertConfigs");

            migrationBuilder.DropTable(
                name: "ApiRequestLog");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "ArkaDalje");

            migrationBuilder.DropTable(
                name: "ArkaHyrje");

            migrationBuilder.DropTable(
                name: "ArkaHyrjeDalje");

            migrationBuilder.DropTable(
                name: "Arkat");

            migrationBuilder.DropTable(
                name: "ArticleVariants");

            migrationBuilder.DropTable(
                name: "Artikujt");

            migrationBuilder.DropTable(
                name: "ATK_CorporateTaxDeclarations");

            migrationBuilder.DropTable(
                name: "ATK_PayrollTaxDeclarations");

            migrationBuilder.DropTable(
                name: "ATK_TVShDeclarations");

            migrationBuilder.DropTable(
                name: "ATK_WithholdingTaxDeclarations");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Borxhi");

            migrationBuilder.DropTable(
                name: "BusinessSettings");

            migrationBuilder.DropTable(
                name: "CashShifts");

            migrationBuilder.DropTable(
                name: "Currencies");

            migrationBuilder.DropTable(
                name: "DeliveryOrders");

            migrationBuilder.DropTable(
                name: "DitariD");

            migrationBuilder.DropTable(
                name: "DitariH");

            migrationBuilder.DropTable(
                name: "EmployeeSchedules");

            migrationBuilder.DropTable(
                name: "Filiala");

            migrationBuilder.DropTable(
                name: "FurnitoriNew");

            migrationBuilder.DropTable(
                name: "GiftCards");

            migrationBuilder.DropTable(
                name: "GiftCardTransactions");

            migrationBuilder.DropTable(
                name: "HR_EmployeeCertificates");

            migrationBuilder.DropTable(
                name: "HR_EmploymentContracts");

            migrationBuilder.DropTable(
                name: "HR_LeaveRequests");

            migrationBuilder.DropTable(
                name: "HR_PayrollRecords");

            migrationBuilder.DropTable(
                name: "Kategoria");

            migrationBuilder.DropTable(
                name: "KategoriaPos");

            migrationBuilder.DropTable(
                name: "KitchenOrders");

            migrationBuilder.DropTable(
                name: "Kompania");

            migrationBuilder.DropTable(
                name: "LlojiShpenzimeve");

            migrationBuilder.DropTable(
                name: "LoyaltyAccounts");

            migrationBuilder.DropTable(
                name: "LoyaltyConfig");

            migrationBuilder.DropTable(
                name: "LoyaltyTransactions");

            migrationBuilder.DropTable(
                name: "MetodaPagese");

            migrationBuilder.DropTable(
                name: "NjesitMatese");

            migrationBuilder.DropTable(
                name: "NotificationItems");

            migrationBuilder.DropTable(
                name: "OfflineQueueLog");

            migrationBuilder.DropTable(
                name: "POSUsers");

            migrationBuilder.DropTable(
                name: "POSUsers_Legacy");

            migrationBuilder.DropTable(
                name: "PriceRules");

            migrationBuilder.DropTable(
                name: "ProductBundles");

            migrationBuilder.DropTable(
                name: "Punetoret");

            migrationBuilder.DropTable(
                name: "PurchaseItems");

            migrationBuilder.DropTable(
                name: "PurchaseOrderItems");

            migrationBuilder.DropTable(
                name: "Qytetet");

            migrationBuilder.DropTable(
                name: "ReceiptItems");

            migrationBuilder.DropTable(
                name: "RentalAgreements");

            migrationBuilder.DropTable(
                name: "RentalItems");

            migrationBuilder.DropTable(
                name: "RestaurantTables");

            migrationBuilder.DropTable(
                name: "ReturnReceiptItems");

            migrationBuilder.DropTable(
                name: "SalesBookEntries");

            migrationBuilder.DropTable(
                name: "Sektori");

            migrationBuilder.DropTable(
                name: "TableSections");

            migrationBuilder.DropTable(
                name: "Tatimi");

            migrationBuilder.DropTable(
                name: "ZReports");

            migrationBuilder.DropTable(
                name: "HR_Employees");

            migrationBuilder.DropTable(
                name: "Purchases");

            migrationBuilder.DropTable(
                name: "PurchaseOrders");

            migrationBuilder.DropTable(
                name: "Articles");

            migrationBuilder.DropTable(
                name: "Receipts");

            migrationBuilder.DropTable(
                name: "ReturnReceipts");

            migrationBuilder.DropTable(
                name: "BusinessPartners");
        }
    }
}
