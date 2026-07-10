using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.BMDData
{
    /// <summary>
    /// Maps to SQL Server BMDData.DitariH table
    /// Albanian: DitariH = Daily Purchase Register (Diary of Purchases - Hyrje=Entry)
    /// Each row is a purchase line item (similar to PurchaseItem)
    /// </summary>
    [Table("DitariH")]
    public class DitariH
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
        public long? Numri { get; set; } // Document Number
        
        [Column("KUPONI")]
        [StringLength(50)]
        public string? Kuponi { get; set; } // Coupon
        
        [Required]
        [Column("NrFatures")]
        [StringLength(50)]
        public string NrFatures { get; set; } = string.Empty; // Invoice Number
        
        [Required]
        [Column("NrDUD")]
        [StringLength(50)]
        public string NrDUD { get; set; } = string.Empty; // DUD Number
        
        [Column("FILIALA")]
        public int? Filiala { get; set; } // Branch ID
        
        [Column("SUBJEKTI")]
        public int? Subjekti { get; set; } // Supplier/Subject ID
        
        [Column("PUNETORI")]
        [StringLength(100)]
        public string? Punetori { get; set; } // Worker
        
        [Column("TIPI")]
        [StringLength(50)]
        public string? Tipi { get; set; } // Type (Vendore, Import, etc.)
        
        [Column("BARKODI")]
        [StringLength(50)]
        public string? Barkodi { get; set; } // Barcode
        
        [Column("ARTIKULLI")]
        [StringLength(250)]
        public string? Artikulli { get; set; } // Article Name
        
        [Column("NJESIA")]
        [StringLength(20)]
        public string? Njesia { get; set; } // Unit
        
        [Column("Sasia")]
        public double? Sasia { get; set; } // Quantity
        
        [Column("Cmimi_Furn")]
        public double? CmimiFurn { get; set; } // Purchase Price
        
        [Column("Rabati_Per")]
        public double? RabatiPer { get; set; } // Discount Percent
        
        [Column("Rabati_Vl")]
        public double? RabatiVl { get; set; } // Discount Value
        
        [Column("Vlera_Furn")]
        public double? VleraFurn { get; set; } // Purchase Value
        
        [Column("Transporti_Vl")]
        public double? TransportiVl { get; set; } // Transport Value
        
        [Column("Shpenzimet_Vl")]
        public double? ShpenzimetVl { get; set; } // Expenses Value
        
        [Column("Baza_Per_Dogane")]
        public double? BazaPerDogane { get; set; } // Base for Customs
        
        [Column("Dogana_Per")]
        public double? DoganaPer { get; set; } // Customs Percent
        
        [Column("Dogana_Vl")]
        public double? DoganaVl { get; set; } // Customs Value
        
        [Column("Aksiza_Vl")]
        public double? AksizaVl { get; set; } // Excise Value
        
        [Column("Tvsh_Vl")]
        public double? TvshVl { get; set; } // VAT Value
        
        [Column("Tvsh_Per")]
        public double? TvshPer { get; set; } // VAT Percent
        
        [Column("Cmimi_Kushtues")]
        public double? CmimiKushtues { get; set; } // Cost Price
        
        [Column("Vlera_Kushtuese")]
        public double? VleraKushtuese { get; set; } // Cost Value
        
        [Column("Marzha_Per")]
        public double? MarzhaPer { get; set; } // Margin Percent
        
        [Column("Cm_Shitjes")]
        public double? CmShitjes { get; set; } // Sales Price
        
        [Column("Cm_Shitjes_1")]
        public double? CmShitjes1 { get; set; } // Sales Price 1
        
        [Column("Vlera_Me_Tvsh")]
        public double? VleraMeTvsh { get; set; } // Value with VAT
        
        [Column("Perpunimi")]
        [StringLength(50)]
        public string? Perpunimi { get; set; } // Processing
        
        [Column("VEREJTJE")]
        [StringLength(500)]
        public string? Verejtje { get; set; } // Notes
        
        [Column("PerPagese")]
        public double? PerPagese { get; set; } // For Payment
        
        [Column("Pagoi")]
        public double? Pagoi { get; set; } // Paid
        
        [Column("Mbeti")]
        public double? Mbeti { get; set; } // Left
        
        [Column("TextMetoda")]
        public int? TextMetoda { get; set; } // Payment Method
        
        [Column("Artikulli_ID")]
        public int? ArtikullId { get; set; } // Article ID
        
        [Column("Nr_Rendor")]
        public int? NrRendor { get; set; } // Order Number
        
        [Column("Cm_Me_TVSH")]
        public double? CmMeTvsh { get; set; } // Price with VAT
        
        [Column("TVSH_Jo_Zbritshme")]
        public bool? TvshJoZbritshme { get; set; } // Non-deductible VAT
        
        [Column("Tarifa")]
        public int? Tarifa { get; set; } // Tariff
        
        [Column("Lloji")]
        public int? Lloji { get; set; } // Type
        
        [Column("Vl_Pas_Doganes")]
        public double? VlPasDoganes { get; set; } // Value after Customs
        
        // Computed properties for compatibility
        [NotMapped]
        public string Barcode => Barkodi ?? string.Empty;
        
        [NotMapped]
        public string ArticleName => Artikulli ?? string.Empty;
        
        [NotMapped]
        public decimal Quantity => (decimal)(Sasia ?? 0);
        
        [NotMapped]
        public decimal PurchasePrice => (decimal)(CmimiFurn ?? 0);
        
        [NotMapped]
        public decimal TotalValue => (decimal)(VleraMeTvsh ?? VleraFurn ?? 0);
        
        [NotMapped]
        public decimal VATRate => (decimal)(TvshPer ?? 18);
        
        [NotMapped]
        public decimal VATValue => (decimal)(TvshVl ?? 0);
    }
}
