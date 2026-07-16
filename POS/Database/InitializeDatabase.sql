-- KosovaPOS Database Initialization Script
-- This script creates the BMDData database and initializes essential tables

USE master
GO

-- Create database if not exists
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'BMDData')
BEGIN
    CREATE DATABASE [BMDData]
    ON PRIMARY
    (
        NAME = N'BMDData',
        FILENAME = N'$(MSSQL_DATA_PATH)\BMDData.mdf',
        SIZE = 10MB,
        MAXSIZE = UNLIMITED,
        FILEGROWTH = 10%
    )
    LOG ON
    (
        NAME = N'BMDData_log',
        FILENAME = N'$(MSSQL_DATA_PATH)\BMDData_log.ldf',
        SIZE = 10MB,
        MAXSIZE = UNLIMITED,
        FILEGROWTH = 10%
    )
END
GO

USE [BMDData]
GO

-- Set database properties
ALTER DATABASE [BMDData] SET RECOVERY SIMPLE
ALTER DATABASE [BMDData] SET COMPATIBILITY_LEVEL = 150
GO

-- Create POSUsers table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[POSUsers]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[POSUsers]
    (
        [UserId] INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
        [Username] NVARCHAR(50) NOT NULL UNIQUE,
        [PasswordHash] NVARCHAR(MAX) NOT NULL,
        [FullName] NVARCHAR(100),
        [Role] NVARCHAR(20) NOT NULL DEFAULT 'User',
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [LastLogin] DATETIME NULL,
        [Email] NVARCHAR(100) NULL
    )
    
    CREATE INDEX IX_POSUsers_Username ON [dbo].[POSUsers]([Username])
    CREATE INDEX IX_POSUsers_Role ON [dbo].[POSUsers]([Role])
    
    -- Insert default admin user
    INSERT INTO [dbo].[POSUsers] ([Username], [PasswordHash], [FullName], [Role])
    VALUES (
        'admin',
        'AQAAAAIAAYagAAAAEJ8Y/PqE9JqX7Fa+EcaQ4pwBY6qpqKxD/jcnEeYKiBnxOlJU6YqWgAX/rR7TfqVxfQ==', -- 'admin' hashed
        'Administrator',
        'Admin'
    )
END
GO

-- Create Articles/Products table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Articles]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Articles]
    (
        [ArticleId] INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
        [ArticleCode] NVARCHAR(50) NOT NULL UNIQUE,
        [ArticleName] NVARCHAR(200) NOT NULL,
        [Category] NVARCHAR(100) NOT NULL,
        [UnitPrice] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [VAT] DECIMAL(5,2) NOT NULL DEFAULT 18,
        [Quantity] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [ModifiedDate] DATETIME DEFAULT GETDATE()
    )
    
    CREATE INDEX IX_Articles_Code ON [dbo].[Articles]([ArticleCode])
    CREATE INDEX IX_Articles_Category ON [dbo].[Articles]([Category])
END
GO

-- Create Sales table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Sales]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Sales]
    (
        [SaleId] INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
        [SaleNumber] NVARCHAR(50) NOT NULL UNIQUE,
        [UserId] INT NOT NULL REFERENCES [dbo].[POSUsers]([UserId]),
        [SaleDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [TotalAmount] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [VAT] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [PaymentMethod] NVARCHAR(50) NOT NULL,
        [Notes] NVARCHAR(500) NULL,
        [IsReturned] BIT NOT NULL DEFAULT 0,
        [FiscalCode] NVARCHAR(50) NULL
    )
    
    CREATE INDEX IX_Sales_Date ON [dbo].[Sales]([SaleDate])
    CREATE INDEX IX_Sales_User ON [dbo].[Sales]([UserId])
    CREATE INDEX IX_Sales_Number ON [dbo].[Sales]([SaleNumber])
END
GO

-- Create SalesDetails table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SalesDetails]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[SalesDetails]
    (
        [DetailId] INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
        [SaleId] INT NOT NULL REFERENCES [dbo].[Sales]([SaleId]) ON DELETE CASCADE,
        [ArticleId] INT NOT NULL REFERENCES [dbo].[Articles]([ArticleId]),
        [Quantity] DECIMAL(18,2) NOT NULL,
        [UnitPrice] DECIMAL(18,2) NOT NULL,
        [Discount] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [VAT] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [TotalAmount] DECIMAL(18,2) NOT NULL
    )
    
    CREATE INDEX IX_SalesDetails_Sale ON [dbo].[SalesDetails]([SaleId])
    CREATE INDEX IX_SalesDetails_Article ON [dbo].[SalesDetails]([ArticleId])
END
GO

-- Create Configuration table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Configuration]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Configuration]
    (
        [ConfigId] INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
        [ConfigKey] NVARCHAR(100) NOT NULL UNIQUE,
        [ConfigValue] NVARCHAR(MAX) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [ModifiedDate] DATETIME DEFAULT GETDATE()
    )
    
    INSERT INTO [dbo].[Configuration] ([ConfigKey], [ConfigValue], [Description])
    VALUES
        ('BusinessName', 'Kosovo Business', 'Business name for receipts and reports'),
        ('BusinessAddress', 'Prishtina, Kosovo', 'Business address'),
        ('TaxId', '', 'Business tax ID'),
        ('DefaultVAT', '18', 'Default VAT percentage'),
        ('DatabaseVersion', '1.0.0', 'Current database schema version'),
        ('FiscalEnabled', 'true', 'Enable fiscal printer integration'),
        ('ReceiptFormat', 'A4', 'Receipt paper format'),
        ('CurrencyCode', 'EUR', 'Currency code for transactions')
END
GO

-- Create AuditLog table for security
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AuditLog]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AuditLog]
    (
        [AuditId] INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
        [UserId] INT REFERENCES [dbo].[POSUsers]([UserId]),
        [Action] NVARCHAR(100) NOT NULL,
        [TableName] NVARCHAR(100) NOT NULL,
        [RecordId] INT NOT NULL,
        [OldValues] NVARCHAR(MAX) NULL,
        [NewValues] NVARCHAR(MAX) NULL,
        [ActionDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [IpAddress] NVARCHAR(50) NULL
    )
    
    CREATE INDEX IX_AuditLog_Date ON [dbo].[AuditLog]([ActionDate])
    CREATE INDEX IX_AuditLog_User ON [dbo].[AuditLog]([UserId])
END
GO

-- Create stored procedures for common operations
IF OBJECT_ID('sp_GetDailySales', 'P') IS NULL
    EXEC sp_executesql N'
    CREATE PROCEDURE [dbo].[sp_GetDailySales]
        @SaleDate DATE
    AS
    BEGIN
        SELECT
            s.[SaleId],
            s.[SaleNumber],
            s.[SaleDate],
            u.[FullName] AS [Cashier],
            s.[TotalAmount],
            s.[VAT],
            s.[PaymentMethod],
            (SELECT COUNT(*) FROM [dbo].[SalesDetails] WHERE [SaleId] = s.[SaleId]) AS [ItemCount]
        FROM [dbo].[Sales] s
        INNER JOIN [dbo].[POSUsers] u ON s.[UserId] = u.[UserId]
        WHERE CAST(s.[SaleDate] AS DATE) = @SaleDate
        ORDER BY s.[SaleDate] DESC
    END'
GO

IF OBJECT_ID('sp_GetInventoryStatus', 'P') IS NULL
    EXEC sp_executesql N'
    CREATE PROCEDURE [dbo].[sp_GetInventoryStatus]
    AS
    BEGIN
        SELECT
            [ArticleId],
            [ArticleCode],
            [ArticleName],
            [Category],
            [Quantity],
            [UnitPrice],
            [Quantity] * [UnitPrice] AS [TotalValue],
            CASE
                WHEN [Quantity] = 0 THEN ''Out of Stock''
                WHEN [Quantity] < 10 THEN ''Low Stock''
                ELSE ''In Stock''
            END AS [Status]
        FROM [dbo].[Articles]
        WHERE [IsActive] = 1
        ORDER BY [Category], [ArticleName]
    END'
GO

PRINT 'Database initialization completed successfully!'
PRINT 'Tables created: POSUsers, Articles, Sales, SalesDetails, Configuration, AuditLog'
PRINT 'Stored procedures created: sp_GetDailySales, sp_GetInventoryStatus'
