-- Initialize Pizzeria Database Schema and Sample Data

-- Create PizzaToppings table if not exists
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PizzaToppings')
BEGIN
    CREATE TABLE PizzaToppings (
        Id INT PRIMARY KEY IDENTITY(1,1),
        Name NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500),
        Price DECIMAL(10,2) NOT NULL,
        Category NVARCHAR(50),
        IsAvailable BIT NOT NULL DEFAULT 1,
        Icon NVARCHAR(10),
        DisplayOrder INT NOT NULL DEFAULT 0,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );

    -- Insert sample toppings
    INSERT INTO PizzaToppings (Name, Description, Price, Category, Icon, DisplayOrder, IsAvailable) VALUES
    ('Salami', 'Salami premium italiane', 1.50, 'Mish', '??', 1, 1),
    ('Proshutë', 'Proshutë e pjekur', 1.50, 'Mish', '??', 2, 1),
    ('Suxhuk', 'Suxhuk picant', 1.20, 'Mish', '???', 3, 1),
    ('Mish Pule', 'Mish pule të grilluar', 1.80, 'Mish', '??', 4, 1),
    ('Mozzarella', 'Djathë Mozzarella ekstra', 1.00, 'Djathë', '??', 5, 1),
    ('Djathë Gorgonzola', 'Djathë blu italian', 2.00, 'Djathë', '??', 6, 1),
    ('Parmesan', 'Parmesan i thërrmuar', 1.50, 'Djathë', '??', 7, 1),
    ('Kërpudha', 'Kërpudha të freskëta', 0.80, 'Perime', '??', 8, 1),
    ('Ullinj', 'Ullinj të zinj', 0.60, 'Perime', '??', 9, 1),
    ('Speca të kuq', 'Speca të pjekur', 0.80, 'Perime', '??', 10, 1),
    ('Qepë', 'Qepë e freskët', 0.50, 'Perime', '??', 11, 1),
    ('Domate cherry', 'Domate cherry të freskëta', 0.80, 'Perime', '??', 12, 1),
    ('Rukola', 'Rukola e freskët', 0.80, 'Perime', '??', 13, 1),
    ('Majdanoz', 'Majdanoz i freskët', 0.40, 'Perime', '??', 14, 1),
    ('Salcë BBQ', 'Salcë BBQ amerikane', 0.50, 'Salcë', '??', 15, 1),
    ('Salcë Ranch', 'Salcë ranch e shtëpisë', 0.50, 'Salcë', '??', 16, 1);
END
GO

-- Create PizzaCategories table if not exists
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PizzaCategories')
BEGIN
    CREATE TABLE PizzaCategories (
        Id INT PRIMARY KEY IDENTITY(1,1),
        Name NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500),
        Icon NVARCHAR(10),
        DisplayOrder INT NOT NULL DEFAULT 0,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );

    -- Insert sample categories
    INSERT INTO PizzaCategories (Name, Description, Icon, DisplayOrder, IsActive) VALUES
    ('?? Pica Klasike', 'Picat tradicionale me përbërës klasikë', '??', 1, 1),
    ('?? Pica Premium', 'Picat me përbërës premium dhe të veçantë', '?', 2, 1),
    ('?? Pica Vegjane', 'Picat pa mish, vetëm perime', '??', 3, 1),
    ('?? Sallatat', 'Sallata të freskëta dhe të shëndetshme', '??', 4, 1),
    ('?? Pasta', 'Pjata të ndryshme të pastës italiane', '??', 5, 1),
    ('?? Pije', 'Pije të freskëta dhe të ngrohta', '??', 6, 1),
    ('?? Deserte', 'Deserte dhe ëmbëlsira', '??', 7, 1);
END
GO

-- Create PizzaOrderToppings junction table if not exists
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PizzaOrderToppings')
BEGIN
    CREATE TABLE PizzaOrderToppings (
        Id INT PRIMARY KEY IDENTITY(1,1),
        ReceiptItemId INT NOT NULL,
        ToppingId INT NOT NULL,
        Quantity DECIMAL(10,2) NOT NULL DEFAULT 1,
        Price DECIMAL(10,2) NOT NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        FOREIGN KEY (ToppingId) REFERENCES PizzaToppings(Id)
    );
END
GO

-- Insert sample pizzas into Artikujt if table is empty or has very few items
IF (SELECT COUNT(*) FROM Artikujt WHERE Kategoria LIKE '%Pica%') < 5
BEGIN
    -- Pica Klasike
    INSERT INTO Artikujt (Barkodi, Emertimi, NjesiaP, NjesiaSH, Kategoria, CFurnizimit, Marzha, CShitjes, CShitjes1, CShumices, Vat, Tatimi, Sasia, PaBarkod)
    VALUES 
    ('PIZZA001', 'Pica Margherita', 'Copë', 'Copë', '?? Pica Klasike', 2.50, 100, 5.00, 7.00, 9.00, 3, 18, 0, 'Y'),
    ('PIZZA002', 'Pica Proshutë', 'Copë', 'Copë', '?? Pica Klasike', 3.00, 100, 6.00, 8.00, 10.00, 3, 18, 0, 'Y'),
    ('PIZZA003', 'Pica Salami', 'Copë', 'Copë', '?? Pica Klasike', 3.00, 100, 6.00, 8.00, 10.00, 3, 18, 0, 'Y'),
    ('PIZZA004', 'Pica Vegetariane', 'Copë', 'Copë', '?? Pica Vegjane', 2.80, 100, 5.50, 7.50, 9.50, 3, 18, 0, 'Y'),
    ('PIZZA005', 'Pica Quattro Formaggi', 'Copë', 'Copë', '?? Pica Premium', 3.50, 100, 7.00, 9.00, 11.00, 3, 18, 0, 'Y'),
    
    -- Pica Premium
    ('PIZZA006', 'Pica Capricciosa', 'Copë', 'Copë', '?? Pica Premium', 3.80, 100, 7.50, 9.50, 11.50, 3, 18, 0, 'Y'),
    ('PIZZA007', 'Pica Diavola (Picante)', 'Copë', 'Copë', '?? Pica Premium', 3.60, 100, 7.00, 9.00, 11.00, 3, 18, 0, 'Y'),
    ('PIZZA008', 'Pica Marinara', 'Copë', 'Copë', '?? Pica Klasike', 2.80, 100, 5.50, 7.50, 9.50, 3, 18, 0, 'Y'),
    ('PIZZA009', 'Pica BBQ Chicken', 'Copë', 'Copë', '?? Pica Premium', 4.00, 100, 8.00, 10.00, 12.00, 3, 18, 0, 'Y'),
    ('PIZZA010', 'Pica Hawaian', 'Copë', 'Copë', '?? Pica Klasike', 3.20, 100, 6.50, 8.50, 10.50, 3, 18, 0, 'Y'),
    
    -- Pije
    ('DRINK001', 'Coca Cola 0.5L', 'Copë', 'Copë', '?? Pije', 0.50, 100, 1.50, 1.50, 1.50, 3, 18, 100, 'N'),
    ('DRINK002', 'Ujë Mineral 0.5L', 'Copë', 'Copë', '?? Pije', 0.30, 100, 1.00, 1.00, 1.00, 3, 18, 100, 'N'),
    ('DRINK003', 'Fanta 0.5L', 'Copë', 'Copë', '?? Pije', 0.50, 100, 1.50, 1.50, 1.50, 3, 18, 100, 'N'),
    ('DRINK004', 'Sprite 0.5L', 'Copë', 'Copë', '?? Pije', 0.50, 100, 1.50, 1.50, 1.50, 3, 18, 100, 'N'),
    
    -- Sallata dhe të tjera
    ('SALAD001', 'Sallata Caesar', 'Copë', 'Copë', '?? Sallatat', 2.00, 100, 4.00, 4.00, 4.00, 3, 18, 0, 'Y'),
    ('SALAD002', 'Sallata Greke', 'Copë', 'Copë', '?? Sallatat', 2.20, 100, 4.50, 4.50, 4.50, 3, 18, 0, 'Y'),
    ('PASTA001', 'Pasta Carbonara', 'Copë', 'Copë', '?? Pasta', 2.50, 100, 5.00, 5.00, 5.00, 3, 18, 0, 'Y'),
    ('PASTA002', 'Pasta Bolognese', 'Copë', 'Copë', '?? Pasta', 2.80, 100, 5.50, 5.50, 5.50, 3, 18, 0, 'Y'),
    ('DESSERT001', 'Tiramisu', 'Copë', 'Copë', '?? Deserte', 1.50, 100, 3.00, 3.00, 3.00, 3, 18, 0, 'Y'),
    ('DESSERT002', 'Panna Cotta', 'Copë', 'Copë', '?? Deserte', 1.20, 100, 2.50, 2.50, 2.50, 3, 18, 0, 'Y');
    
    PRINT 'Sample pizzeria data inserted successfully!';
END
ELSE
BEGIN
    PRINT 'Pizzeria data already exists, skipping insert.';
END
GO

PRINT 'Pizzeria database initialization completed!';
GO
