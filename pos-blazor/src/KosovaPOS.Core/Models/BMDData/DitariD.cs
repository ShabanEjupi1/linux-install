using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.BMDData
{
    /// <summary>
    /// Maps to SQL Server BMDData.DitariD table
    /// Albanian: DitariD = Daily Sales Register (Diary of Sales - Detail)
    /// Each row is a sale line item (similar to ReceiptItem)
    /// </summary>
    [Table("DitariD")]
    public class DitariD
    {
        [Key]
        [Column("ID")]
        public long Id { get; set; }
        
        [Column("DATA")]
        public DateTime? Data { get; set; } // Date
        
        [Column("ORA")]
        [StringLength(10)]
        public string? Ora { get; set; } // Time
        
        [Column("NUMRI")]
        public long? Numri { get; set; } // Receipt Number
        
        [Column("KUPONI")]
        [StringLength(50)]
        public string? Kuponi { get; set; } // Coupon/Fiscal number
        
        [Column("ARKA")]
        [StringLength(50)]
        public string? Arka { get; set; } // Cash Register
        
        [Column("SEKTORI")]
        [StringLength(50)]
        public string? Sektori { get; set; } // Sector
        
        [Column("SUBJEKTI")]
        public int? Subjekti { get; set; } // Customer/Subject ID
        
        [Column("PUNETORI")]
        [StringLength(100)]
        public string? Punetori { get; set; } // Worker/Cashier
        
        [Column("MUAJI")]
        [StringLength(10)]
        public string? Muaji { get; set; } // Month
        
        [Column("VITI")]
        [StringLength(10)]
        public string? Viti { get; set; } // Year
        
        [Column("VEREJTJE")]
        [StringLength(500)]
        public string? Verejtje { get; set; } // Notes
        
        [Column("BARKODI")]
        [StringLength(50)]
        public string? Barkodi { get; set; } // Barcode
        
        [Column("ARTIKULLI")]
        [StringLength(250)]
        public string? Artikulli { get; set; } // Article Name
        
        [Column("NJESIA")]
        [StringLength(20)]
        public string? Njesia { get; set; } // Unit
        
        [Column("SASIA")]
        public double? Sasia { get; set; } // Quantity
        
        [Column("QMIMI")]
        public double? Qmimi { get; set; } // Price
        
        [Column("QMIMI1")]
        public double? Qmimi1 { get; set; } // Price 1 (alternative)
        
        [Column("QMIMIPATVSH")]
        public double? QmimiPaTvsh { get; set; } // Price without VAT
        
        [Column("QMIMIPATVSH1")]
        public double? QmimiPaTvsh1 { get; set; } // Price without VAT 1
        
        [Column("QMIMIF")]
        public double? QmimiF { get; set; } // Purchase Price
        
        [Column("RABATI")]
        public double? Rabati { get; set; } // Discount Percent
        
        [Column("VLERARABATIT")]
        public double? VleraRabatit { get; set; } // Discount Value
        
        [Column("VLERARABATIT1")]
        public double? VleraRabatit1 { get; set; } // Discount Value 1
        
        [Column("TVSH")]
        public double? Tvsh { get; set; } // VAT
        
        [Column("TVSH1")]
        public double? Tvsh1 { get; set; } // VAT 1
        
        [Column("VLERAPATVSH")]
        public double? VleraPaTvsh { get; set; } // Value without VAT
        
        [Column("VLERAPATVSH1")]
        public double? VleraPaTvsh1 { get; set; } // Value without VAT 1
        
        [Column("VLERAMETVSH")]
        public double? VleraMeTvsh { get; set; } // Value with VAT
        
        [Column("VLERAMETVSH1")]
        public double? VleraMeTvsh1 { get; set; } // Value with VAT 1
        
        [Column("BMD")]
        [StringLength(10)]
        public string? Bmd { get; set; } // BMD code
        
        [Column("PAKETIMI")]
        public double? Paketimi { get; set; } // Packaging
        
        [Column("QMIMISHUMICES")]
        public double? QmimiShumices { get; set; } // Wholesale Price
        
        [Column("FILIALA")]
        [StringLength(50)]
        public string? Filiala { get; set; } // Branch
        
        [Column("PERPUNIMI")]
        [StringLength(50)]
        public string? Perpunimi { get; set; } // Processing status
        
        [Column("KATEGORIA")]
        [StringLength(150)]
        public string? Kategoria { get; set; } // Category
        
        [Column("NrFiskalKlient")]
        [StringLength(50)]
        public string? NrFiskalKlient { get; set; } // Customer Fiscal Number
        
        [Column("AdresaKlient")]
        [StringLength(200)]
        public string? AdresaKlient { get; set; } // Customer Address
        
        [Column("ShifraKlient")]
        [StringLength(50)]
        public string? ShifraKlient { get; set; } // Customer Code
        
        [Column("PAGOI")]
        public double? Pagoi { get; set; } // Paid Amount
        
        [Column("MBETI")]
        public double? Mbeti { get; set; } // Left Amount
        
        [Column("chk")]
        [StringLength(10)]
        public string? Chk { get; set; } // Check
        
        [Column("MUAJINR")]
        public long? MuajiNr { get; set; } // Month Number
        
        [Column("VAT")]
        public double? Vat { get; set; } // VAT Rate
        
        [Column("MetodaP")]
        public int? MetodaP { get; set; } // Payment Method ID
        
        [Column("Banka")]
        [StringLength(100)]
        public string? Banka { get; set; } // Bank
        
        [Column("tatimi")]
        public double? Tatimi { get; set; } // Tax
        
        [Column("tatiminr")]
        public double? TatimiNr { get; set; } // Tax Number
        
        [Column("Artikulli_ID")]
        public int? ArtikullId { get; set; } // Article ID (FK)
        
        [Column("Nr_Rendor")]
        public int? NrRendor { get; set; } // Order Number
        
        [Column("TavolinaID")]
        public int? TavolinaId { get; set; } // Table ID (restaurant)
        
        [Column("StatusiRestorant")]
        public bool? StatusiRestorant { get; set; } // Restaurant Status
        
        [Column("Pershkrimi")]
        [StringLength(500)]
        public string? Pershkrimi { get; set; } // Description
        
        [Column("Vetura")]
        public int? Vetura { get; set; } // Vehicle ID
        
        [Column("Parcela")]
        [StringLength(50)]
        public string? Parcela { get; set; } // Parcel
        
        // Computed properties for compatibility
        [NotMapped]
        public string Barcode => Barkodi ?? string.Empty;
        
        [NotMapped]
        public string ArticleName => Artikulli ?? string.Empty;
        
        [NotMapped]
        public decimal Quantity => (decimal)(Sasia ?? 0);
        
        [NotMapped]
        public decimal Price => (decimal)(Qmimi ?? 0);
        
        [NotMapped]
        public decimal TotalValue => (decimal)(VleraMeTvsh ?? 0);
        
        [NotMapped]
        public decimal DiscountPercent => (decimal)(Rabati ?? 0);
        
        [NotMapped]
        public decimal DiscountValue => (decimal)(VleraRabatit ?? 0);
        
        [NotMapped]
        public decimal VATRate => (decimal)(Vat ?? Tatimi ?? 18);
        
        [NotMapped]
        public decimal VATValue => (decimal)(Tvsh ?? 0);
    }
}
