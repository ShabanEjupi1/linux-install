-- Migration script to add missing columns to POSUsers table
-- Database: BMDData
-- Execute this script in SQL Server Management Studio or your preferred SQL client

USE BMDData;
GO

-- Check if POSUsers table exists, if not create it
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'POSUsers')
BEGIN
    CREATE TABLE POSUsers (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Username NVARCHAR(100) NOT NULL,
        PasswordHash NVARCHAR(256) NOT NULL,
        Email NVARCHAR(100) NULL,
        FullName NVARCHAR(200) NOT NULL,
        Role NVARCHAR(50) NOT NULL DEFAULT 'Cashier',
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        LastLogin DATETIME NULL,
        Branch NVARCHAR(200) NULL,
        PhoneNumber NVARCHAR(50) NULL,
        CanManageArticles BIT NOT NULL DEFAULT 1,
        CanManagePurchases BIT NOT NULL DEFAULT 0,
        CanManageUsers BIT NOT NULL DEFAULT 0,
        CanViewReports BIT NOT NULL DEFAULT 1,
        CanModifyPrices BIT NOT NULL DEFAULT 0,
        CanDeleteReceipts BIT NOT NULL DEFAULT 0,
        CanGiveDiscounts BIT NOT NULL DEFAULT 0,
        MaxDiscountPercent DECIMAL(5,2) NOT NULL DEFAULT 0
    );
    
    PRINT 'POSUsers table created successfully.';
END
ELSE
BEGIN
    PRINT 'POSUsers table already exists. Adding missing columns...';
    
    -- Add Branch column if it doesn't exist
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('POSUsers') AND name = 'Branch')
    BEGIN
        ALTER TABLE POSUsers ADD Branch NVARCHAR(200) NULL;
        PRINT 'Added column: Branch';
    END
    
    -- Add Email column if it doesn't exist
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('POSUsers') AND name = 'Email')
    BEGIN
        ALTER TABLE POSUsers ADD Email NVARCHAR(100) NULL;
        PRINT 'Added column: Email';
    END
    
    -- Add PhoneNumber column if it doesn't exist
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('POSUsers') AND name = 'PhoneNumber')
    BEGIN
        ALTER TABLE POSUsers ADD PhoneNumber NVARCHAR(50) NULL;
        PRINT 'Added column: PhoneNumber';
    END
    
    -- Add LastLogin column if it doesn't exist
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('POSUsers') AND name = 'LastLogin')
    BEGIN
        ALTER TABLE POSUsers ADD LastLogin DATETIME NULL;
        PRINT 'Added column: LastLogin';
    END
    
    -- Add PasswordHash column if it doesn't exist
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('POSUsers') AND name = 'PasswordHash')
    BEGIN
        ALTER TABLE POSUsers ADD PasswordHash NVARCHAR(256) NOT NULL DEFAULT '';
        PRINT 'Added column: PasswordHash';
    END
    
    -- Add CanManageArticles column if it doesn't exist
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('POSUsers') AND name = 'CanManageArticles')
    BEGIN
        ALTER TABLE POSUsers ADD CanManageArticles BIT NOT NULL DEFAULT 1;
        PRINT 'Added column: CanManageArticles';
    END
    
    -- Add CanManagePurchases column if it doesn't exist
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('POSUsers') AND name = 'CanManagePurchases')
    BEGIN
        ALTER TABLE POSUsers ADD CanManagePurchases BIT NOT NULL DEFAULT 0;
        PRINT 'Added column: CanManagePurchases';
    END
    
    -- Add CanManageUsers column if it doesn't exist
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('POSUsers') AND name = 'CanManageUsers')
    BEGIN
        ALTER TABLE POSUsers ADD CanManageUsers BIT NOT NULL DEFAULT 0;
        PRINT 'Added column: CanManageUsers';
    END
    
    -- Add CanViewReports column if it doesn't exist
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('POSUsers') AND name = 'CanViewReports')
    BEGIN
        ALTER TABLE POSUsers ADD CanViewReports BIT NOT NULL DEFAULT 1;
        PRINT 'Added column: CanViewReports';
    END
    
    -- Add CanModifyPrices column if it doesn't exist
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('POSUsers') AND name = 'CanModifyPrices')
    BEGIN
        ALTER TABLE POSUsers ADD CanModifyPrices BIT NOT NULL DEFAULT 0;
        PRINT 'Added column: CanModifyPrices';
    END
    
    -- Add CanDeleteReceipts column if it doesn't exist
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('POSUsers') AND name = 'CanDeleteReceipts')
    BEGIN
        ALTER TABLE POSUsers ADD CanDeleteReceipts BIT NOT NULL DEFAULT 0;
        PRINT 'Added column: CanDeleteReceipts';
    END
    
    -- Add CanGiveDiscounts column if it doesn't exist
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('POSUsers') AND name = 'CanGiveDiscounts')
    BEGIN
        ALTER TABLE POSUsers ADD CanGiveDiscounts BIT NOT NULL DEFAULT 0;
        PRINT 'Added column: CanGiveDiscounts';
    END
    
    -- Add MaxDiscountPercent column if it doesn't exist
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('POSUsers') AND name = 'MaxDiscountPercent')
    BEGIN
        ALTER TABLE POSUsers ADD MaxDiscountPercent DECIMAL(5,2) NOT NULL DEFAULT 0;
        PRINT 'Added column: MaxDiscountPercent';
    END
END
GO

-- Create default admin user if no users exist
IF NOT EXISTS (SELECT * FROM POSUsers)
BEGIN
    -- Default password is "admin123" - CHANGE THIS AFTER FIRST LOGIN
    -- BCrypt hash for "admin123"
    INSERT INTO POSUsers (Username, PasswordHash, Email, FullName, Role, IsActive, CreatedAt, Branch, PhoneNumber,
                          CanManageArticles, CanManagePurchases, CanManageUsers, CanViewReports, 
                          CanModifyPrices, CanDeleteReceipts, CanGiveDiscounts, MaxDiscountPercent)
    VALUES ('admin', '$2a$11$vZ5FeQhQJ0KzJx9bXjLxUOXJZZz5qzR5bYqXqZXqBqHqXqZqXqZqX', 
            'admin@example.com', 'Administrator', 'Admin', 1, GETDATE(), NULL, NULL,
            1, 1, 1, 1, 1, 1, 1, 100);
    
    PRINT 'Default admin user created. Username: admin, Password: admin123 (CHANGE THIS!)';
END
GO

-- Verify all columns exist
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'POSUsers'
ORDER BY ORDINAL_POSITION;
GO

PRINT 'Migration completed successfully!';
