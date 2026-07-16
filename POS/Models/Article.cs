using System;
using System.ComponentModel.DataAnnotations;

namespace KosovaPOS.Models
{
    public class Article
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Barcode { get; set; } = string.Empty;
        
        [Required]
        [StringLength(250)]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(20)]
        public string Unit { get; set; } = "Copë"; // Copë, KG, M, etc.
        
        [StringLength(100)]
        public string? SalesUnit { get; set; } // Njësia Shitjes
        
        public decimal Pack { get; set; } = 1; // Paketimi
        
        [Required]
        public decimal PurchasePrice { get; set; } // Çmimi Furnizimit
        
        public decimal Margin { get; set; } = 0; // Marzha
        
        public decimal PackagePrice { get; set; } = 0; // Çmimi Paketimit
        
        public decimal WholesalePrice { get; set; } = 0; // Çmimi Shumicës
        
        [Required]
        public decimal SalesPrice { get; set; } // Çmimi Shitjes

        public decimal SalesPrice1 { get; set; } = 0; // Çmimi Shitjes 1 (alternative)

        /// <summary>
        /// Pizza pricing for small size (e.g., 25cm)
        /// </summary>
        public decimal? PizzaPriceSmall { get; set; }

        /// <summary>
        /// Pizza pricing for medium size (e.g., 30cm)
        /// </summary>
        public decimal? PizzaPriceMedium { get; set; }

        /// <summary>
        /// Pizza pricing for large size (e.g., 35cm)
        /// </summary>
        public decimal? PizzaPriceLarge { get; set; }

        public decimal VATRate { get; set; } = 18; // Kosovo standard VAT (18%, 8%, 0%)
        
        public int VATType { get; set; } = 3; // 3=18%, 2=8%, 1=0%
        
        // PLU (Product Look-Up) number from fiscal printer device
        // This must match the PLU in the fiscal printer's Articles.dbf file
        public int? PLU { get; set; } = null;
        
        [StringLength(150)]
        public string? Category { get; set; } // Kategoria (Tekstil, Rroba, etc.)
        
        [StringLength(200)]
        public string? Supplier { get; set; } // Furnitori
        
        public int SupplierId { get; set; } = 0;
        
        // Retail/Wholesale specific fields
        [StringLength(50)]
        public string? Size { get; set; } // Madhësia
        
        [StringLength(50)]
        public string? Color { get; set; } // Ngjyra
        
        [StringLength(100)]
        public string? Brand { get; set; } // Prodhuesi
        
        [StringLength(100)]
        public string? Importer { get; set; } // Importuesi
        
        [StringLength(100)]
        public string? Location { get; set; } // Vendi (store location/shelf)
        
        [StringLength(50)]
        public string? Sector { get; set; } // Sektori
        
        [StringLength(50)]
        public string? Branch { get; set; } // Filiala
        
        [StringLength(50)]
        public string? Season { get; set; } // Sezoni (Verë, Dimër, Pranverë, Vjeshtë)
        
        [StringLength(20)]
        public string? Gender { get; set; } // Gjinia (Meshkuj, Femra, Fëmijë, Unisex)
        
        [StringLength(500)]
        public string? Notes { get; set; } // Vërejtje
        
        public decimal StockQuantity { get; set; } = 0; // Sasia
        
        public decimal StockIn { get; set; } = 0; // Sasia Hyrje
        
        public decimal StockOut { get; set; } = 0; // Sasia Dalje
        
        public decimal AverageSalesPrice { get; set; } = 0; // Çmimi Mesatar Shitës
        
        public decimal AveragePurchasePrice { get; set; } = 0; // Çmimi Mesatar Furnizues
        
        public decimal MinimumStock { get; set; } = 0;
        
        public DateTime? ExpiryDate { get; set; } // Afati
        
        public bool HasBarcode { get; set; } = true; // PaBarkod (false=Y, true=N)
        
        public bool IsRegular { get; set; } = true; // IRregullt
        
        public bool IsWeighed { get; set; } = false; // Peshon (për peshoren)
        
        public bool IsActive { get; set; } = true;
        
        public int ProductType { get; set; } = 1; // Tipi (1=Normal, 2=Service, etc.)
        
        public int? POSCategoryId { get; set; } // KategoriaPos_ID
        
        [StringLength(500)]
        public string? PhotoPath { get; set; } // Foto
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        
        // ========= PIZZERIA-SPECIFIC FIELDS =========
        
        /// <summary>
        /// Indicates if this article is a pizza
        /// </summary>
        public bool IsPizza { get; set; } = false;
        
        /// <summary>
        /// Pizza size: Small, Medium, Large, XLarge
        /// </summary>
        [StringLength(20)]
        public string? PizzaSize { get; set; }
        
        /// <summary>
        /// Base price for the pizza (before toppings)
        /// </summary>
        public decimal BasePrice { get; set; } = 0;
        
        /// <summary>
        /// Indicates if pizza can be customized with toppings
        /// </summary>
        public bool IsCustomizable { get; set; } = false;
        
        /// <summary>
        /// Available toppings for this pizza (comma-separated)
        /// </summary>
        [StringLength(1000)]
        public string? AvailableToppings { get; set; }
        
        /// <summary>
        /// Default toppings included in base price (comma-separated)
        /// </summary>
        [StringLength(1000)]
        public string? DefaultToppings { get; set; }
        
        /// <summary>
        /// Crust type: Thin, Regular, Thick, Stuffed
        /// </summary>
        [StringLength(50)]
        public string? CrustType { get; set; }
        
        /// <summary>
        /// Preparation time in minutes
        /// </summary>
        public int PreparationTime { get; set; } = 15;
        
        /// <summary>
        /// Menu category for pizzeria: Pizza, Appetizer, Beverage, Dessert, Salad, Pasta
        /// </summary>
        [StringLength(100)]
        public string? MenuCategory { get; set; }
        
        /// <summary>
        /// Indicates if item is currently available
        /// </summary>
        public bool IsAvailable { get; set; } = true;
        
        /// <summary>
        /// Special dietary info: Vegetarian, Vegan, Gluten-Free, Spicy, etc.
        /// </summary>
        [StringLength(200)]
        public string? DietaryInfo { get; set; }
    }
}
