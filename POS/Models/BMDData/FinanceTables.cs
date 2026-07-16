using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.BMDData
{
    /// <summary>
    /// Maps to SQL Server BMDData.Borxhi table
    /// Albanian: Borxhi = Debt/Credit
    /// Tracks customer debts and payments
    /// Actual schema: id, Data, KlientiID, Klienti, Shuma, Paguar, Mbetur, Pershkrimi, PunetoriID, Filiala
    /// </summary>
    [Table("Borxhi")]
    public class Borxhi
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
        
        [Column("Shuma")]
        public double? Shuma { get; set; } // Total Amount
        
        [Column("Paguar")]
        public double? Paguar { get; set; } // Paid Amount
        
        [Column("Mbetur")]
        public double? Mbetur { get; set; } // Remaining Amount
        
        [Column("Pershkrimi")]
        [StringLength(500)]
        public string? Pershkrimi { get; set; } // Description
        
        [Column("PunetoriID")]
        public int? PunetoriID { get; set; } // Worker ID
        
        [Column("Filiala")]
        public int? Filiala { get; set; } // Branch ID
        
        // Computed properties
        [NotMapped]
        public string CustomerName => Klienti ?? string.Empty;
        
        [NotMapped]
        public decimal TotalAmount => (decimal)(Shuma ?? 0);
        
        [NotMapped]
        public decimal PaidAmount => (decimal)(Paguar ?? 0);
        
        [NotMapped]
        public decimal RemainingAmount => (decimal)(Mbetur ?? 0);
        
        // Old field names for legacy code
        [NotMapped]
        public double? Vlera => Shuma;
        
        [NotMapped]
        public double? Pagoi => Paguar;
        
        [NotMapped]
        public double? Mbeti => Mbetur;
    }
    
    /// <summary>
    /// Maps to SQL Server BMDData.ArkaHyrje table
    /// Albanian: ArkaHyrje = Cash Income
    /// Records cash income transactions
    /// Actual schema: id, Data, ArkaID, Shuma, Pershkrimi, PunetoriID, Filiala
    /// </summary>
    [Table("ArkaHyrje")]
    public class ArkaHyrje
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Column("Data")]
        public DateTime? Data { get; set; } // Date
        
        [Column("ArkaID")]
        public int? ArkaID { get; set; } // Cash Register ID
        
        [Column("Shuma")]
        public double? Shuma { get; set; } // Amount
        
        [Column("Pershkrimi")]
        [StringLength(500)]
        public string? Pershkrimi { get; set; } // Description
        
        [Column("PunetoriID")]
        public int? PunetoriID { get; set; } // Worker ID
        
        [Column("Filiala")]
        public int? Filiala { get; set; } // Branch ID
        
        // Computed properties
        [NotMapped]
        public string Description => Pershkrimi ?? string.Empty;
        
        [NotMapped]
        public decimal Amount => (decimal)(Shuma ?? 0);
    }
    
    /// <summary>
    /// Maps to SQL Server BMDData.ArkaDalje table
    /// Albanian: ArkaDalje = Cash Expense
    /// Records cash expense transactions
    /// Actual schema: id, Data, ArkaID, Shuma, Pershkrimi, PunetoriID, Filiala
    /// </summary>
    [Table("ArkaDalje")]
    public class ArkaDalje
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Column("Data")]
        public DateTime? Data { get; set; } // Date
        
        [Column("ArkaID")]
        public int? ArkaID { get; set; } // Cash Register ID
        
        [Column("Shuma")]
        public double? Shuma { get; set; } // Amount
        
        [Column("Pershkrimi")]
        [StringLength(500)]
        public string? Pershkrimi { get; set; } // Description
        
        [Column("PunetoriID")]
        public int? PunetoriID { get; set; } // Worker ID
        
        [Column("Filiala")]
        public int? Filiala { get; set; } // Branch ID
        
        // Computed properties
        [NotMapped]
        public string Description => Pershkrimi ?? string.Empty;
        
        [NotMapped]
        public decimal Amount => (decimal)(Shuma ?? 0);
    }
    
    /// <summary>
    /// Maps to SQL Server BMDData.ArkaHyrjeDalje table
    /// Albanian: ArkaHyrjeDalje = Cash In/Out Combined
    /// Records all cash register transactions (income and expenses)
    /// Actual schema: id, Data, ArkaID, Lloji, Shuma, Pershkrimi, PunetoriID, Filiala
    /// </summary>
    [Table("ArkaHyrjeDalje")]
    public class ArkaHyrjeDalje
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Column("Data")]
        public DateTime? Data { get; set; } // Date
        
        [Column("ArkaID")]
        public int? ArkaID { get; set; } // Cash Register ID
        
        [Column("Lloji")]
        [StringLength(50)]
        public string? Lloji { get; set; } // Type (Hyrje/Dalje - Income/Expense)
        
        [Column("Shuma")]
        public double? Shuma { get; set; } // Amount
        
        [Column("Pershkrimi")]
        [StringLength(500)]
        public string? Pershkrimi { get; set; } // Description
        
        [Column("PunetoriID")]
        public int? PunetoriID { get; set; } // Worker ID
        
        [Column("Filiala")]
        public int? Filiala { get; set; } // Branch ID
        
        // Computed properties
        [NotMapped]
        public string Description => Pershkrimi ?? string.Empty;
        
        [NotMapped]
        public string TransactionType => Lloji ?? string.Empty;
        
        [NotMapped]
        public decimal Amount => (decimal)(Shuma ?? 0);
        
        [NotMapped]
        public bool IsIncome => Lloji?.Contains("Hyrje") ?? false;
        
        [NotMapped]
        public bool IsExpense => Lloji?.Contains("Dalje") ?? false;
        
        // Old field names for legacy code
        [NotMapped]
        public string? DOK => Lloji;
        
        [NotMapped]
        public double? VleraH => IsIncome ? Shuma : 0;
        
        [NotMapped]
        public double? VleraD => IsExpense ? Shuma : 0;
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
