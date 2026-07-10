using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.BMDData
{
    /// <summary>
    /// Maps to SQL Server BMDData.Artikujt table
    /// Albanian: Artikujt = Articles/Products
    /// </summary>
    [Table("Artikujt")]
    public class Artikujt
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Required]
        [Column("Barkodi")]
        [StringLength(50)]
        public string Barkodi { get; set; } = string.Empty; // Barcode
        
        [Required]
        [Column("Emertimi")]
        [StringLength(250)]
        public string Emertimi { get; set; } = string.Empty; // Name
        
        [Column("NjesiaP")]
        [StringLength(20)]
        public string? NjesiaP { get; set; } // Unit (Purchase)
        
        [Column("NjesiaSH")]
        [StringLength(100)]
        public string? NjesiaSH { get; set; } // Unit (Sales)
        
        [Column("Kategoria")]
        [StringLength(150)]
        public string? Kategoria { get; set; } // Category
        
        [Column("PaBarkod")]
        [StringLength(1)]
        public string? PaBarkod { get; set; } // Without Barcode (Y/N)
        
        [Column("IRregullt")]
        [StringLength(1)]
        public string? IRregullt { get; set; } // Irregular
        
        [Column("CFurnizimit")]
        public double? CFurnizimit { get; set; } // Purchase Price
        
        [Column("Marzha")]
        public double? Marzha { get; set; } // Margin
        
        [Column("Paketimi")]
        public double? Paketimi { get; set; } // Pack
        
        [Column("CPaketimit")]
        public double? CPaketimit { get; set; } // Package Price
        
        [Column("CShumices")]
        public double? CShumices { get; set; } // Wholesale Price
        
        [Column("CShitjes")]
        public double? CShitjes { get; set; } // Sales Price
        
        [Column("CShitjes1")]
        public double? CShitjes1 { get; set; } // Sales Price 1 (alternative)
        
        [Column("Sasia")]
        public double? Sasia { get; set; } // Stock Quantity
        
        [Column("Afati")]
        public DateTime? Afati { get; set; } // Expiry Date
        
        [Column("Furnitori")]
        public int? Furnitori { get; set; } // Supplier ID
        
        [Column("Verejtje")]
        [StringLength(500)]
        public string? Verejtje { get; set; } // Notes
        
        [Column("Filiala")]
        public int? Filiala { get; set; } // Branch ID
        
        [Column("Sektori")]
        [StringLength(50)]
        public string? Sektori { get; set; } // Sector
        
        [Column("SasiaHyrje")]
        public double? SasiaHyrje { get; set; } // Stock In
        
        [Column("SasiaDalje")]
        public double? SasiaDalje { get; set; } // Stock Out
        
        [Column("CMesatarShites")]
        public double? CMesatarShites { get; set; } // Average Sales Price
        
        [Column("CMesatarFurnizues")]
        public double? CMesatarFurnizues { get; set; } // Average Purchase Price
        
        [Column("Vendi")]
        [StringLength(100)]
        public string? Vendi { get; set; } // Location
        
        [Column("Prodhuesi")]
        [StringLength(100)]
        public string? Prodhuesi { get; set; } // Brand/Manufacturer
        
        [Column("Importuesi")]
        [StringLength(100)]
        public string? Importuesi { get; set; } // Importer
        
        [Column("tatiminr")]
        public double? TatimiNr { get; set; } // Tax Number
        
        [Column("Tatimi")]
        public double? Tatimi { get; set; } // Tax Rate
        
        [Column("Shpenzim")]
        public bool? Shpenzim { get; set; } // Is Expense
        
        [Column("Peshore")]
        public bool? Peshore { get; set; } // Is Weighed
        
        [Column("Vat")]
        public int? Vat { get; set; } // VAT Type (3=18%, 2=8%, 1=0%)
        
        [Column("Tipi")]
        public int? Tipi { get; set; } // Product Type
        
        [Column("Foto")]
        public byte[]? Foto { get; set; } // Photo
        
        [Column("PhotoPath")]
        [StringLength(500)]
        public string? PhotoPath { get; set; } // Photo path for webstore
        
        [Column("KategoriaPos_ID")]
        public int? KategoriaPosId { get; set; } // POS Category ID
        
        // Computed properties to map to existing Article model interface
        [NotMapped]
        public string Barcode => Barkodi;
        
        [NotMapped]
        public string Name => Emertimi;
        
        [NotMapped]
        public string Unit => NjesiaP ?? "Copë";
        
        [NotMapped]
        public decimal PurchasePrice => (decimal)(CFurnizimit ?? 0);
        
        [NotMapped]
        public decimal SalesPrice => (decimal)(CShitjes ?? 0);
        
        [NotMapped]
        public decimal StockQuantity => (decimal)(Sasia ?? 0);
        
        [NotMapped]
        public string? Category => Kategoria;
        
        [NotMapped]
        public string? Brand => Prodhuesi;
        
        [NotMapped]
        public string? Supplier => null; // Need to join with FurnitoriNew
        
        [NotMapped]
        public decimal VATRate => KosovoVat.Resolve(Vat, Tatimi);
        
        [NotMapped]
        public bool IsActive => true; // Default active
    }
}
