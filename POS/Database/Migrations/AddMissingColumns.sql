-- ============================================================================
-- Migration Script: Add Missing Columns to Restaurant Management Tables
-- Description: Adds columns that exist in models but missing in database
-- Date: 2024
-- ============================================================================

USE BMDData;
GO

-- ============================================================================
-- INVENTORY MANAGEMENT FIXES
-- ============================================================================

-- Add MovementDate to InventoryMovements if it doesn't exist
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'InventoryMovements' AND COLUMN_NAME = 'MovementDate')
BEGIN
    ALTER TABLE InventoryMovements 
    ADD MovementDate DATETIME NOT NULL DEFAULT GETDATE();
    PRINT 'Added MovementDate column to InventoryMovements';
END
ELSE
BEGIN
    PRINT 'MovementDate column already exists in InventoryMovements';
END
GO

-- Add Balance to InventorySuppliers if it doesn't exist
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'InventorySuppliers' AND COLUMN_NAME = 'Balance')
BEGIN
    ALTER TABLE InventorySuppliers 
    ADD Balance DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'Added Balance column to InventorySuppliers';
END
ELSE
BEGIN
    PRINT 'Balance column already exists in InventorySuppliers';
END
GO

-- Add Message to StockAlerts if it doesn't exist
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'StockAlerts' AND COLUMN_NAME = 'Message')
BEGIN
    ALTER TABLE StockAlerts 
    ADD Message NVARCHAR(500) NOT NULL DEFAULT '';
    PRINT 'Added Message column to StockAlerts';
END
ELSE
BEGIN
    PRINT 'Message column already exists in StockAlerts';
END
GO

-- Add ThresholdQuantity to StockAlerts if it doesn't exist
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'StockAlerts' AND COLUMN_NAME = 'ThresholdQuantity')
BEGIN
    ALTER TABLE StockAlerts 
    ADD ThresholdQuantity DECIMAL(18,3) NOT NULL DEFAULT 0;
    PRINT 'Added ThresholdQuantity column to StockAlerts';
END
ELSE
BEGIN
    PRINT 'ThresholdQuantity column already exists in StockAlerts';
END
GO

-- ============================================================================
-- RESTAURANT TABLE MANAGEMENT FIXES
-- ============================================================================

-- Add CurrentReceiptId to RestaurantTables if it doesn't exist
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'RestaurantTables' AND COLUMN_NAME = 'CurrentReceiptId')
BEGIN
    ALTER TABLE RestaurantTables 
    ADD CurrentReceiptId INT NULL;
    PRINT 'Added CurrentReceiptId column to RestaurantTables';
END
ELSE
BEGIN
    PRINT 'CurrentReceiptId column already exists in RestaurantTables';
END
GO

-- ============================================================================
-- KITCHEN DISPLAY SYSTEM FIXES
-- ============================================================================

-- Add CustomerName to KitchenOrders if it doesn't exist
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'KitchenOrders' AND COLUMN_NAME = 'CustomerName')
BEGIN
    ALTER TABLE KitchenOrders 
    ADD CustomerName NVARCHAR(100) NULL;
    PRINT 'Added CustomerName column to KitchenOrders';
END
ELSE
BEGIN
    PRINT 'CustomerName column already exists in KitchenOrders';
END
GO

-- ============================================================================
-- LOYALTY PROGRAM FIXES
-- ============================================================================

-- Verify CampaignType exists in Campaigns and has correct definition
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Campaigns' AND COLUMN_NAME = 'CampaignType')
BEGIN
    ALTER TABLE Campaigns 
    ADD CampaignType NVARCHAR(50) NOT NULL DEFAULT 'Discount';
    PRINT 'Added CampaignType column to Campaigns';
END
ELSE
BEGIN
    PRINT 'CampaignType column already exists in Campaigns';
    
    -- Check if it needs to be resized from NVARCHAR(20) to NVARCHAR(50)
    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'Campaigns' 
        AND COLUMN_NAME = 'CampaignType' 
        AND CHARACTER_MAXIMUM_LENGTH < 50)
    BEGIN
        ALTER TABLE Campaigns 
        ALTER COLUMN CampaignType NVARCHAR(50) NOT NULL;
        PRINT 'Resized CampaignType column in Campaigns to NVARCHAR(50)';
    END
END
GO

-- Add missing columns to LoyaltyTransactions if needed
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'LoyaltyTransactions' AND COLUMN_NAME = 'CustomerName')
BEGIN
    ALTER TABLE LoyaltyTransactions 
    ADD CustomerName NVARCHAR(100) NOT NULL DEFAULT '';
    PRINT 'Added CustomerName column to LoyaltyTransactions';
END
ELSE
BEGIN
    PRINT 'CustomerName column already exists in LoyaltyTransactions';
END
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'LoyaltyTransactions' AND COLUMN_NAME = 'Amount')
BEGIN
    ALTER TABLE LoyaltyTransactions 
    ADD Amount DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'Added Amount column to LoyaltyTransactions';
END
ELSE
BEGIN
    PRINT 'Amount column already exists in LoyaltyTransactions';
END
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'LoyaltyTransactions' AND COLUMN_NAME = 'TransactionDate')
BEGIN
    ALTER TABLE LoyaltyTransactions 
    ADD TransactionDate DATETIME NOT NULL DEFAULT GETDATE();
    PRINT 'Added TransactionDate column to LoyaltyTransactions';
END
ELSE
BEGIN
    PRINT 'TransactionDate column already exists in LoyaltyTransactions';
END
GO

-- ============================================================================
-- ADDITIONAL CAMPAIGN FIELDS
-- ============================================================================

-- Add missing campaign fields that exist in the model but might be missing
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Campaigns' AND COLUMN_NAME = 'PointsRequired')
BEGIN
    ALTER TABLE Campaigns 
    ADD PointsRequired INT NOT NULL DEFAULT 0;
    PRINT 'Added PointsRequired column to Campaigns';
END
ELSE
BEGIN
    PRINT 'PointsRequired column already exists in Campaigns';
END
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Campaigns' AND COLUMN_NAME = 'DiscountAmount')
BEGIN
    ALTER TABLE Campaigns 
    ADD DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'Added DiscountAmount column to Campaigns';
END
ELSE
BEGIN
    PRINT 'DiscountAmount column already exists in Campaigns';
END
GO

-- ============================================================================
-- VERIFICATION QUERIES
-- ============================================================================

PRINT '';
PRINT '============================================================================';
PRINT 'VERIFICATION - Checking all modified tables';
PRINT '============================================================================';
PRINT '';

PRINT 'InventoryMovements columns:';
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'InventoryMovements'
ORDER BY ORDINAL_POSITION;

PRINT '';
PRINT 'InventorySuppliers columns:';
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'InventorySuppliers'
ORDER BY ORDINAL_POSITION;

PRINT '';
PRINT 'StockAlerts columns:';
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'StockAlerts'
ORDER BY ORDINAL_POSITION;

PRINT '';
PRINT 'RestaurantTables columns:';
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'RestaurantTables'
ORDER BY ORDINAL_POSITION;

PRINT '';
PRINT 'KitchenOrders columns:';
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'KitchenOrders'
ORDER BY ORDINAL_POSITION;

PRINT '';
PRINT 'Campaigns columns:';
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Campaigns'
ORDER BY ORDINAL_POSITION;

PRINT '';
PRINT '============================================================================';
PRINT 'Migration Complete!';
PRINT '============================================================================';
GO
