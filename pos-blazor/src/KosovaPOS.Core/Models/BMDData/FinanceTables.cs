using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.BMDData
{
    /// <summary>
    /// Maps to SQL Server BMDData.Borxhi table
    /// Albanian: Borxhi = Debt/Credit
    /// Tracks customer debts and payments
    /// </summary>
    [Table("Borxhi")]
    public class Borxhi
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Column("Klienti")]
        [StringLength(200)]
        public string? Klienti { get; set; } // Customer Name
        
        [Column("Data")]
        public DateTime? Data { get; set; } // Date
        
        [Column("Vlera")]
        public double? Vlera { get; set; } // Total Value
        
        [Column("Pagoi")]
        public double? Pagoi { get; set; } // Paid Amount
        
        [Column("Mbeti")]
        public double? Mbeti { get; set; } // Remaining
        
        [Column("Pershkrimi")]
        [StringLength(500)]
        public string? Pershkrimi { get; set; } // Description
        
        // Computed properties
        [NotMapped]
        public string CustomerName => Klienti ?? string.Empty;
        
        [NotMapped]
        public decimal TotalValue => (decimal)(Vlera ?? 0);
        
        [NotMapped]
        public decimal PaidAmount => (decimal)(Pagoi ?? 0);
        
        [NotMapped]
        public decimal RemainingAmount => (decimal)(Mbeti ?? 0);
    }
    
    /// <summary>
    /// Maps to SQL Server BMDData.ArkaHyrje table
    /// Albanian: ArkaHyrje = Cash Income
    /// Records cash income transactions
    /// </summary>
    [Table("ArkaHyrje")]
    public class ArkaHyrje
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Column("Pershkrimi")]
        [StringLength(500)]
        public string? Pershkrimi { get; set; } // Description
        
        [Column("Punetori")]
        [StringLength(100)]
        public string? Punetori { get; set; } // Worker
        
        [Column("Data")]
        public DateTime? Data { get; set; } // Date
        
        [Column("Vlera")]
        public double? Vlera { get; set; } // Value
        
        // Computed properties
        [NotMapped]
        public string Description => Pershkrimi ?? string.Empty;
        
        [NotMapped]
        public string Worker => Punetori ?? string.Empty;
        
        [NotMapped]
        public decimal Amount => (decimal)(Vlera ?? 0);
    }
    
    /// <summary>
    /// Maps to SQL Server BMDData.ArkaDalje table
    /// Albanian: ArkaDalje = Cash Expense
    /// Records cash expense transactions
    /// </summary>
    [Table("ArkaDalje")]
    public class ArkaDalje
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Column("Pershkrimi")]
        [StringLength(500)]
        public string? Pershkrimi { get; set; } // Description
        
        [Column("Punetori")]
        [StringLength(100)]
        public string? Punetori { get; set; } // Worker
        
        [Column("Data")]
        public DateTime? Data { get; set; } // Date
        
        [Column("Vlera")]
        public double? Vlera { get; set; } // Value
        
        // Computed properties
        [NotMapped]
        public string Description => Pershkrimi ?? string.Empty;
        
        [NotMapped]
        public string Worker => Punetori ?? string.Empty;
        
        [NotMapped]
        public decimal Amount => (decimal)(Vlera ?? 0);
    }
    
    /// <summary>
    /// Maps to SQL Server BMDData.ArkaHyrjeDalje table
    /// Albanian: ArkaHyrjeDalje = Cash In/Out Combined
    /// This is the MAIN table containing all cash transactions (sales and purchases)
    /// VleraH = Income (Hyrje), VleraD = Expense (Dalje)
    /// </summary>
    [Table("ArkaHyrjeDalje")]
    public class ArkaHyrjeDalje
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        
        [Column("Pershkrimi")]
        [StringLength(500)]
        public string? Pershkrimi { get; set; } // Description (e.g., "SHITJA ME KUPON", "BLERJA VENDORE")
        
        [Column("DOK")]
        [StringLength(50)]
        public string? DOK { get; set; } // Document number (e.g., "01-KO3986", "01-BV340")
        
        [Column("Subjekti")]
        public int? Subjekti { get; set; } // Subject/Partner ID
        
        [Column("Data")]
        public DateTime? Data { get; set; } // Date
        
        [Column("MetodaP")]
        public int? MetodaP { get; set; } // Payment Method (1=Cash)
        
        [Column("VleraH")]
        public double? VleraH { get; set; } // Income Value (Hyrje)
        
        [Column("VleraD")]
        public double? VleraD { get; set; } // Expense Value (Dalje)
        
        // Computed properties
        [NotMapped]
        public string Description => Pershkrimi ?? string.Empty;
        
        [NotMapped]
        public string DocumentNumber => DOK ?? string.Empty;
        
        [NotMapped]
        public decimal IncomeAmount => (decimal)(VleraH ?? 0);
        
        [NotMapped]
        public decimal ExpenseAmount => (decimal)(VleraD ?? 0);
        
        [NotMapped]
        public bool IsSale => Pershkrimi?.Contains("SHITJA") ?? false;
        
        [NotMapped]
        public bool IsPurchase => Pershkrimi?.Contains("BLERJA") ?? false;
        
        [NotMapped]
        public string TransactionType => IsSale ? "Shitje" : IsPurchase ? "Blerje" : "Tjetër";
    }
    
    /// <summary>
    /// Maps to SQL Server BMDData.Qytetet table
    /// Albanian: Qytetet = Cities
    /// </summary>
    [Table("Qytetet")]
    public class Qytetet
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }
        
        [Column("Emertimi")]
        [StringLength(100)]
        public string? Emertimi { get; set; } // Name
        
        [NotMapped]
        public string Name => Emertimi ?? string.Empty;
    }
    
    /// <summary>
    /// Maps to SQL Server BMDData.NjesitMatese table
    /// Albanian: Njësi Matëse = Units of Measurement
    /// </summary>
    [Table("NjesitMatese")]
    public class NjesitMatese
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Column("Emri")]
        [StringLength(50)]
        public string? Emri { get; set; } // Name
        
        [Column("Pershkrimi")]
        [StringLength(200)]
        public string? Pershkrimi { get; set; } // Description
        
        [NotMapped]
        public string Name => Emri ?? string.Empty;
    }
    
    /// <summary>
    /// Maps to SQL Server BMDData.LlojiShpenzimeve table
    /// Albanian: Lloji i Shpenzimeve = Expense Types
    /// </summary>
    [Table("LlojiShpenzimeve")]
    public class LlojiShpenzimeve
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Column("Emri")]
        [StringLength(100)]
        public string? Emri { get; set; } // Name
        
        [Column("Pershkrimi")]
        [StringLength(500)]
        public string? Pershkrimi { get; set; } // Description
        
        [NotMapped]
        public string Name => Emri ?? string.Empty;
    }
}
