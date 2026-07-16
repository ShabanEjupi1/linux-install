-- Comprehensive Database Fix Script for KosovaPOS
-- This script creates all missing tables and adds missing columns
-- Execute this against your BMDData database

USE BMDData;
GO

-- ==============================================
-- CAMPAIGN TABLE - Add missing columns
-- ==============================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Campaigns]') AND name = 'CampaignType')
BEGIN
    ALTER TABLE Campaigns ADD CampaignType NVARCHAR(50) NULL DEFAULT 'Discount';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Campaigns]') AND name = 'PointsRequired')
BEGIN
    ALTER TABLE Campaigns ADD PointsRequired INT NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Campaigns]') AND name = 'DiscountAmount')
BEGIN
    ALTER TABLE Campaigns ADD DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Campaigns]') AND name = 'Value')
BEGIN
    ALTER TABLE Campaigns ADD [Value] DECIMAL(18,2) NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Campaigns]') AND name = 'ApplicableArticles')
BEGIN
    ALTER TABLE Campaigns ADD ApplicableArticles NVARCHAR(MAX) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Campaigns]') AND name = 'Status')
BEGIN
    ALTER TABLE Campaigns ADD Status NVARCHAR(20) NOT NULL DEFAULT 'Draft';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Campaigns]') AND name = 'UsageCount')
BEGIN
    ALTER TABLE Campaigns ADD UsageCount INT NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Campaigns]') AND name = 'TotalRevenue')
BEGIN
    ALTER TABLE Campaigns ADD TotalRevenue DECIMAL(18,2) NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Campaigns]') AND name = 'UpdatedAt')
BEGIN
    ALTER TABLE Campaigns ADD UpdatedAt DATETIME NOT NULL DEFAULT GETDATE();
END
GO

-- ==============================================
-- LOYALTY TRANSACTIONS - Add missing columns
-- ==============================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[LoyaltyTransactions]') AND name = 'Amount')
BEGIN
    ALTER TABLE LoyaltyTransactions ADD Amount DECIMAL(18,2) NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[LoyaltyTransactions]') AND name = 'PurchaseAmount')
BEGIN
    ALTER TABLE LoyaltyTransactions ADD PurchaseAmount DECIMAL(18,2) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[LoyaltyTransactions]') AND name = 'CreatedAt')
BEGIN
    ALTER TABLE LoyaltyTransactions ADD CreatedAt DATETIME NOT NULL DEFAULT GETDATE();
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[LoyaltyTransactions]') AND name = 'CustomerName')
BEGIN
    ALTER TABLE LoyaltyTransactions ADD CustomerName NVARCHAR(100) NOT NULL DEFAULT '';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[LoyaltyTransactions]') AND name = 'ReferenceNumber')
BEGIN
    ALTER TABLE LoyaltyTransactions ADD ReferenceNumber NVARCHAR(50) NULL;
END
GO

-- ==============================================
-- RESTAURANT TABLES - Add missing columns
-- ==============================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[RestaurantTables]') AND name = 'CurrentReceiptId')
BEGIN
    ALTER TABLE RestaurantTables ADD CurrentReceiptId INT NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[RestaurantTables]') AND name = 'Location')
BEGIN
    ALTER TABLE RestaurantTables ADD Location NVARCHAR(50) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[RestaurantTables]') AND name = 'OccupiedSince')
BEGIN
    ALTER TABLE RestaurantTables ADD OccupiedSince DATETIME NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[RestaurantTables]') AND name = 'ReservedFor')
BEGIN
    ALTER TABLE RestaurantTables ADD ReservedFor NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[RestaurantTables]') AND name = 'ReservedPhone')
BEGIN
    ALTER TABLE RestaurantTables ADD ReservedPhone NVARCHAR(20) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[RestaurantTables]') AND name = 'ReservedUntil')
BEGIN
    ALTER TABLE RestaurantTables ADD ReservedUntil DATETIME NULL;
END
GO

-- ==============================================
-- INVENTORY MOVEMENTS - Add missing columns
-- ==============================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMovements]') AND name = 'ArticleName')
BEGIN
    ALTER TABLE InventoryMovements ADD ArticleName NVARCHAR(100) NOT NULL DEFAULT '';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMovements]') AND name = 'Description')
BEGIN
    ALTER TABLE InventoryMovements ADD Description NVARCHAR(500) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMovements]') AND name = 'Reference')
BEGIN
    ALTER TABLE InventoryMovements ADD Reference NVARCHAR(50) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMovements]') AND name = 'Unit')
BEGIN
    ALTER TABLE InventoryMovements ADD Unit NVARCHAR(20) NOT NULL DEFAULT 'pcs';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMovements]') AND name = 'ReferenceNumber')
BEGIN
    ALTER TABLE InventoryMovements ADD ReferenceNumber NVARCHAR(50) NULL;
END
GO

-- ==============================================
-- KITCHEN ORDERS - Add missing columns
-- ==============================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[KitchenOrders]') AND name = 'CustomerName')
BEGIN
    ALTER TABLE KitchenOrders ADD CustomerName NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[KitchenOrders]') AND name = 'OrderItems')
BEGIN
    ALTER TABLE KitchenOrders ADD OrderItems NVARCHAR(MAX) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[KitchenOrders]') AND name = 'SpecialInstructions')
BEGIN
    ALTER TABLE KitchenOrders ADD SpecialInstructions NVARCHAR(1000) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[KitchenOrders]') AND name = 'Station')
BEGIN
    ALTER TABLE KitchenOrders ADD Station NVARCHAR(50) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[KitchenOrders]') AND name = 'AssignedTo')
BEGIN
    ALTER TABLE KitchenOrders ADD AssignedTo NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[KitchenOrders]') AND name = 'ReceivedAt')
BEGIN
    ALTER TABLE KitchenOrders ADD ReceivedAt DATETIME NOT NULL DEFAULT GETDATE();
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[KitchenOrders]') AND name = 'ReadyAt')
BEGIN
    ALTER TABLE KitchenOrders ADD ReadyAt DATETIME NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[KitchenOrders]') AND name = 'ServedAt')
BEGIN
    ALTER TABLE KitchenOrders ADD ServedAt DATETIME NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[KitchenOrders]') AND name = 'TargetTime')
BEGIN
    ALTER TABLE KitchenOrders ADD TargetTime INT NOT NULL DEFAULT 15;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[KitchenOrders]') AND name = 'ActualTime')
BEGIN
    ALTER TABLE KitchenOrders ADD ActualTime INT NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[KitchenOrders]') AND name = 'UpdatedAt')
BEGIN
    ALTER TABLE KitchenOrders ADD UpdatedAt DATETIME NOT NULL DEFAULT GETDATE();
END
GO

-- ==============================================
-- CREATE MISSING TABLES
-- ==============================================

-- WorkShifts table (Staff Scheduling)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkShifts')
BEGIN
    CREATE TABLE WorkShifts (
        Id INT PRIMARY KEY IDENTITY(1,1),
        EmployeeId INT NOT NULL,
        EmployeeName NVARCHAR(100) NOT NULL,
        ShiftDate DATE NOT NULL,
        StartTime TIME NOT NULL,
        EndTime TIME NOT NULL,
        ShiftType NVARCHAR(20) NOT NULL DEFAULT 'Regular',
        Position NVARCHAR(50) NULL,
        HourlyRate DECIMAL(18,2) NOT NULL DEFAULT 0,
        Status NVARCHAR(20) NOT NULL DEFAULT 'Scheduled',
        ActualStartTime DATETIME NULL,
        ActualEndTime DATETIME NULL,
        BreakMinutes INT NOT NULL DEFAULT 0,
        Notes NVARCHAR(500) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END
GO

-- EmployeePerformance table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EmployeePerformances')
BEGIN
    CREATE TABLE EmployeePerformances (
        Id INT PRIMARY KEY IDENTITY(1,1),
        EmployeeId INT NOT NULL,
        EmployeeName NVARCHAR(100) NOT NULL,
        PeriodStart DATE NOT NULL,
        PeriodEnd DATE NOT NULL,
        TotalSales DECIMAL(18,2) NOT NULL DEFAULT 0,
        TransactionCount INT NOT NULL DEFAULT 0,
        AverageTransactionValue DECIMAL(18,2) NOT NULL DEFAULT 0,
        CustomerSatisfactionScore DECIMAL(3,2) NOT NULL DEFAULT 0,
        AttendanceRate DECIMAL(5,2) NOT NULL DEFAULT 0,
        HoursWorked DECIMAL(10,2) NOT NULL DEFAULT 0,
        TotalWages DECIMAL(18,2) NOT NULL DEFAULT 0,
        PerformanceRating NVARCHAR(20) NULL,
        Notes NVARCHAR(1000) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END
GO

-- TimeCards table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TimeCards')
BEGIN
    CREATE TABLE TimeCards (
        Id INT PRIMARY KEY IDENTITY(1,1),
        EmployeeId INT NOT NULL,
        EmployeeName NVARCHAR(100) NOT NULL,
        ClockInTime DATETIME NOT NULL,
        ClockOutTime DATETIME NULL,
        TotalHours DECIMAL(10,2) NOT NULL DEFAULT 0,
        BreakMinutes INT NOT NULL DEFAULT 0,
        IsApproved BIT NOT NULL DEFAULT 0,
        ApprovedBy NVARCHAR(100) NULL,
        ApprovedAt DATETIME NULL,
        Notes NVARCHAR(500) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END
GO

-- EmployeeAvailability table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EmployeeAvailabilities')
BEGIN
    CREATE TABLE EmployeeAvailabilities (
        Id INT PRIMARY KEY IDENTITY(1,1),
        EmployeeId INT NOT NULL,
        DayOfWeek INT NOT NULL,
        StartTime TIME NOT NULL,
        EndTime TIME NOT NULL,
        IsAvailable BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END
GO

-- TimeOffRequests table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TimeOffRequests')
BEGIN
    CREATE TABLE TimeOffRequests (
        Id INT PRIMARY KEY IDENTITY(1,1),
        EmployeeId INT NOT NULL,
        EmployeeName NVARCHAR(100) NOT NULL,
        RequestType NVARCHAR(50) NOT NULL,
        StartDate DATE NOT NULL,
        EndDate DATE NOT NULL,
        Reason NVARCHAR(1000) NULL,
        Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
        ReviewedBy NVARCHAR(100) NULL,
        ReviewedAt DATETIME NULL,
        ReviewNotes NVARCHAR(500) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END
GO

-- LaborCostAnalyses table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LaborCostAnalyses')
BEGIN
    CREATE TABLE LaborCostAnalyses (
        Id INT PRIMARY KEY IDENTITY(1,1),
        AnalysisDate DATE NOT NULL,
        TotalLaborCost DECIMAL(18,2) NOT NULL DEFAULT 0,
        TotalRevenue DECIMAL(18,2) NOT NULL DEFAULT 0,
        LaborCostPercentage DECIMAL(5,2) NOT NULL DEFAULT 0,
        TotalHours DECIMAL(10,2) NOT NULL DEFAULT 0,
        AverageHourlyRate DECIMAL(18,2) NOT NULL DEFAULT 0,
        EmployeeCount INT NOT NULL DEFAULT 0,
        ProductivityScore DECIMAL(5,2) NOT NULL DEFAULT 0,
        Notes NVARCHAR(1000) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END
GO

PRINT 'Database schema updated successfully!';
GO
