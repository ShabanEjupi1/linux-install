using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.BMDData
{
    /// <summary>
    /// Maps to SQL Server BMDData.DitariD table
    /// Albanian: DitariD = Daily Sales Register Detail (Diary of Sales - Detail)
    /// Each row is a sale line item for a receipt (similar to ReceiptItem)
    /// Actual schema: id, DitariHID, ArtikujID, Barkodi, Emertimi, Sasia, Cmimi, Zbritja, Tatimi, Totali, Vat
    /// </summary>
    [Table("DitariD")]
    public class DitariD
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Column("DitariHID")]
        public long? DitariHID { get; set; } // Foreign key to DitariH (receipt header)
        
        [Column("ArtikujID")]
        public long? ArtikujID { get; set; } // Foreign key to Artikujt (article)
        
        [Column("Barkodi")]
        [StringLength(50)]
        public string? Barkodi { get; set; } // Barcode
        
        [Column("Emertimi")]
        [StringLength(250)]
        public string? Emertimi { get; set; } // Article Name
        
        [Column("Sasia")]
        public double? Sasia { get; set; } // Quantity
        
        [Column("Cmimi")]
        public double? Cmimi { get; set; } // Unit Price
        
        [Column("Zbritja")]
        public double? Zbritja { get; set; } // Discount Amount
        
        [Column("Tatimi")]
        public double? Tatimi { get; set; } // Tax/VAT Amount
        
        [Column("Totali")]
        public double? Totali { get; set; } // Total Amount (line total)
        
        
        [Column("Vat")]
        public double? Vat { get; set; } // VAT Rate (percentage)
        
        // Navigation property
        [ForeignKey("DitariHID")]
        public DitariH? DitariH { get; set; }
        
        // Computed properties for backward compatibility
        [NotMapped]
        public string Barcode => Barkodi ?? string.Empty;
        
        [NotMapped]
        public string ArticleName => Emertimi ?? string.Empty;
        
        [NotMapped]
        public decimal Quantity => (decimal)(Sasia ?? 0);
        
        [NotMapped]
        public decimal UnitPrice => (decimal)(Cmimi ?? 0);
        
        [NotMapped]
        public decimal Discount => (decimal)(Zbritja ?? 0);
        
        [NotMapped]
        public decimal Tax => (decimal)(Tatimi ?? 0);
        
        [NotMapped]
        public decimal Total => (decimal)(Totali ?? 0);
        
        [NotMapped]
        public decimal VATRate => (decimal)(Vat ?? 0);
        
        [NotMapped]
        public long? ReceiptHeaderId => DitariHID;
        
        [NotMapped]
        public long? ArticleId => ArtikujID;
        
        // Old field name mappings for legacy code expecting different schema
        [NotMapped]
        public DateTime? Data => DitariH?.Data;
        
        [NotMapped]
        public string? Ora => DitariH?.Data?.ToString("HH:mm:ss");
        
        [NotMapped]
        public long? Numri => DitariHID;
        
        [NotMapped]
        public string? Kuponi => DitariHID?.ToString();
        
        [NotMapped]
        public string? Kategoria { get; set; } // Category name for analytics
        
        
        [NotMapped]
        public string? Artikulli => Emertimi;
        
        [NotMapped]
        public string? Njesia => "cope"; // Default unit
        
        [NotMapped]
        public double? Qmimi => Cmimi;
        
        [NotMapped]
        public double? QmimiPaTvsh => Cmimi != null && Vat != null ? Cmimi / (1 + Vat / 100) : Cmimi;
        
        [NotMapped]
        public double? QmimiF => Cmimi;
        
        [NotMapped]
        public double? Rabati => Zbritja;
        
        [NotMapped]
        public double? VleraRabatit => Zbritja;
        
        [NotMapped]
        public double? Tvsh => Tatimi;
        
        [NotMapped]
        public double? VleraPaTvsh => Totali != null && Tatimi != null ? Totali - Tatimi : Totali;
        
        [NotMapped]
        public double? VleraMeTvsh => Totali;
        
        [NotMapped]
        public string? Punetori => DitariH?.Punetori;
        
        [NotMapped]
        public string? Viti => DitariH?.Data?.Year.ToString();
        
        [NotMapped]
        public string? Muaji => DitariH?.Data?.ToString("MMMM");
        
        [NotMapped]
        public int? MuajiNr => DitariH?.Data?.Month;
        
        [NotMapped]
        public string? Subjekti => DitariH?.Klienti;
        
        [NotMapped]
        public string? NrFiskalKlient => DitariH?.KlientiID?.ToString();
        
        [NotMapped]
        public string? AdresaKlient => "";
        
        [NotMapped]
        public string? ShifraKlient => DitariH?.KlientiID?.ToString();
        
        [NotMapped]
        public double? Pagoi => DitariH?.Paguar;
        
        [NotMapped]
        public double? Mbeti => DitariH?.Mbetur;
        
        [NotMapped]
        public string? MetodaP => DitariH?.TipiPageses;
        
        [NotMapped]
        public long? ArtikullId => ArtikujID;
        
        [NotMapped]
        public long? NrRendor => Id;
        
        [NotMapped]
        public string? Perpunimi => "";
        
        [NotMapped]
        public double? QmimiShumices => Cmimi;
        
        [NotMapped]
        public string? Verejtje => DitariH?.Verejtje;
    }
}

