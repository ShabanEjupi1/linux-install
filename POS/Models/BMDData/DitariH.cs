using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.BMDData
{
    /// <summary>
    /// Maps to SQL Server BMDData.DitariH table
    /// Albanian: DitariH = Sales Receipt Header (Diary of Sales - Hyrje=Entry)
    /// Each row represents a complete sales receipt/transaction
    /// Actual schema: id, Data, KlientiID, Klienti, ArkaID, Arka, Shuma, Zbritja, Tatimi, Totali, Paguar, Mbetur, PunetoriID, Punetori, Verejtje, Filiala, TipiPageses
    /// </summary>
    [Table("DitariH")]
    public class DitariH
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Column("Data")]
        public DateTime? Data { get; set; } // Date
        
        [Column("KlientiID")]
        public int? KlientiID { get; set; } // Customer ID
        
        [Column("Klienti")]
        [StringLength(250)]
        public string? Klienti { get; set; } // Customer Name
        
        [Column("ArkaID")]
        public int? ArkaID { get; set; } // Cash Register ID
        
        [Column("Arka")]
        [StringLength(100)]
        public string? Arka { get; set; } // Cash Register Name
        
        [Column("Shuma")]
        public double? Shuma { get; set; } // Subtotal
        
        [Column("Zbritja")]
        public double? Zbritja { get; set; } // Discount
        
        [Column("Tatimi")]
        public double? Tatimi { get; set; } // Tax/VAT Amount
        
        [Column("Totali")]
        public double? Totali { get; set; } // Total Amount
        
        [Column("Paguar")]
        public double? Paguar { get; set; } // Paid Amount
        
        [NotMapped]
        public double? Pagoi => Paguar; // Alias for legacy code
        
        [Column("Mbetur")]
        public double? Mbetur { get; set; } // Remaining Amount (Debt)
        
        [NotMapped]
        public double? Mbeti => Mbetur; // Alias for legacy code
        
        [Column("PunetoriID")]
        public int? PunetoriID { get; set; } // Worker/Cashier ID
        
        [Column("Punetori")]
        [StringLength(100)]
        public string? Punetori { get; set; } // Worker/Cashier Name
        
        [Column("Verejtje")]
        [StringLength(500)]
        public string? Verejtje { get; set; } // Notes
        
        [Column("Filiala")]
        public int? Filiala { get; set; } // Branch ID
        
        [Column("TipiPageses")]
        [StringLength(50)]
        public string? TipiPageses { get; set; } // Payment Type
        
        // Computed properties for backward compatibility
        [NotMapped]
        public decimal Subtotal => (decimal)(Shuma ?? 0);
        
        [NotMapped]
        public decimal Discount => (decimal)(Zbritja ?? 0);
        
        [NotMapped]
        public decimal Tax => (decimal)(Tatimi ?? 0);
        
        [NotMapped]
        public decimal Total => (decimal)(Totali ?? 0);
        
        [NotMapped]
        public decimal PaidAmount => (decimal)(Paguar ?? 0);
        
        [NotMapped]
        public decimal RemainingAmount => (decimal)(Mbetur ?? 0);
        
        [NotMapped]
        public string CashierName => Punetori ?? string.Empty;
        
        [NotMapped]
        public string CustomerName => Klienti ?? string.Empty;
        
        // Old field name mappings for legacy code expecting purchase schema fields
        [NotMapped]
        public string? Ora => Data?.ToString("HH:mm:ss");
        
        [NotMapped]
        public long? Numri => Id;
        
        [NotMapped]
        public string? NrFatures => Id.ToString();
        
        [NotMapped]
        public string? NrDUD => "";
        
        [NotMapped]
        public string? Subjekti => Klienti;
        
        [NotMapped]
        public string? Tipi => "Shitje"; // Sales type
        
        [NotMapped]
        public string? Barkodi => ""; // Not applicable for header
        
        [NotMapped]
        public long? ArtikullId => null; // Not applicable for header
        
        [NotMapped]
        public string? Artikulli => ""; // Not applicable for header
        
        [NotMapped]
        public string? Njesia => ""; // Not applicable for header
        
        [NotMapped]
        public double? Sasia => null; // Not applicable for header
        
        [NotMapped]
        public double? CmimiFurn => null; // Not applicable for header
        
        [NotMapped]
        public double? RabatiPer => Zbritja != null && Shuma != null && Shuma > 0 ? (Zbritja / Shuma * 100) : 0;
        
        [NotMapped]
        public double? RabatiVl => Zbritja;
        
        [NotMapped]
        public double? VleraFurn => Shuma;
        
        [NotMapped]
        public double? TvshPer => Tatimi != null && Shuma != null && Shuma > 0 ? (Tatimi / Shuma * 100) : 18;
        
        [NotMapped]
        public double? TvshVl => Tatimi;
        
        [NotMapped]
        public double? VleraMeTvsh => Totali;
        
        [NotMapped]
        public double? CmShitjes => null;
        
        [NotMapped]
        public double? PerPagese => Totali;
        
        [NotMapped]
        public long? NrRendor => Id;
    }
}

