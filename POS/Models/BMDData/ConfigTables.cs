using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.BMDData
{
    /// <summary>
    /// Maps to SQL Server BMDData.Kategoria table
    /// Albanian: Kategoria = Category
    /// </summary>
    [Table("Kategoria")]
    public class Kategoria
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Column("Emri")]
        [StringLength(150)]
        public string? Emri { get; set; } // Name
        
        [Column("Pershkrimi")]
        [StringLength(500)]
        public string? Pershkrimi { get; set; } // Description
        
        [NotMapped]
        public string Name => Emri ?? string.Empty;
    }
    
    /// <summary>
    /// Maps to SQL Server BMDData.KategoriaPos table
    /// POS-specific categories
    /// </summary>
    [Table("KategoriaPos")]
    public class KategoriaPos
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Column("Emri")]
        [StringLength(150)]
        public string? Emri { get; set; } // Name
        
        [Column("Pershkrimi")]
        [StringLength(500)]
        public string? Pershkrimi { get; set; } // Description
        
        [NotMapped]
        public string Name => Emri ?? string.Empty;
    }
    
    /// <summary>
    /// Maps to SQL Server BMDData.Filiala table
    /// Albanian: Filiala = Branch
    /// </summary>
    [Table("Filiala")]
    public class Filiala
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Column("Kompania")]
        public int? Kompania { get; set; } // Company ID
        
        [Column("Emri")]
        [StringLength(200)]
        public string? Emri { get; set; } // Name
        
        [Column("Vendi")]
        [StringLength(100)]
        public string? Vendi { get; set; } // Location
        
        [Column("Menaxheri")]
        [StringLength(200)]
        public string? Menaxheri { get; set; } // Manager
        
        [Column("Data")]
        [StringLength(20)]
        public string? Data { get; set; } // Date
        
        [Column("Pershkrimi")]
        [StringLength(500)]
        public string? Pershkrimi { get; set; } // Description
        
        [NotMapped]
        public string Name => Emri ?? string.Empty;
    }
    
    /// <summary>
    /// Maps to SQL Server BMDData.Kompania table
    /// Albanian: Kompania = Company
    /// </summary>
    [Table("Kompania")]
    public class Kompania
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Column("Kodi")]
        [StringLength(20)]
        public string? Kodi { get; set; } // Code
        
        [Column("NF")]
        [StringLength(50)]
        public string? NF { get; set; } // Fiscal Number
        
        [Column("NIT")]
        [StringLength(50)]
        public string? NIT { get; set; } // NUI
        
        [Column("Tipi")]
        [StringLength(50)]
        public string? Tipi { get; set; } // Type
        
        [Column("Emri")]
        [StringLength(200)]
        public string? Emri { get; set; } // Name
        
        [Column("Vendi")]
        [StringLength(100)]
        public string? Vendi { get; set; } // Location
        
        [Column("Adresa")]
        [StringLength(300)]
        public string? Adresa { get; set; } // Address
        
        [Column("Telefoni")]
        [StringLength(50)]
        public string? Telefoni { get; set; } // Phone
        
        [Column("Pronari")]
        [StringLength(200)]
        public string? Pronari { get; set; } // Owner
        
        [Column("Xhirollogaria")]
        [StringLength(50)]
        public string? Xhirollogaria { get; set; } // Bank Account
        
        [Column("Tvsh")]
        public double? Tvsh { get; set; } // VAT Rate
        
        [Column("Sasia")]
        [StringLength(20)]
        public string? Sasia { get; set; } // Quantity
        
        [NotMapped]
        public string Name => Emri ?? string.Empty;
    }
    
    /// <summary>
    /// Maps to SQL Server BMDData.Sektori table
    /// Albanian: Sektori = Sector
    /// </summary>
    [Table("Sektori")]
    public class Sektori
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Column("Kodi")]
        [StringLength(20)]
        public string? Kodi { get; set; } // Code
        
        [Column("Filiala")]
        [StringLength(50)]
        public string? Filiala { get; set; } // Branch
        
        [Column("Emri")]
        [StringLength(150)]
        public string? Emri { get; set; } // Name
        
        [Column("Vendi")]
        [StringLength(100)]
        public string? Vendi { get; set; } // Location
        
        [Column("Menaxheri")]
        [StringLength(200)]
        public string? Menaxheri { get; set; } // Manager
        
        [Column("Data")]
        [StringLength(20)]
        public string? Data { get; set; } // Date
        
        [Column("Pershkrimi")]
        [StringLength(500)]
        public string? Pershkrimi { get; set; } // Description
        
        [NotMapped]
        public string Name => Emri ?? string.Empty;
    }
    
    /// <summary>
    /// Maps to SQL Server BMDData.Arkat table
    /// Albanian: Arkat = Cash Registers
    /// </summary>
    [Table("Arkat")]
    public class Arkat
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Column("Kodi")]
        [StringLength(20)]
        public string? Kodi { get; set; } // Code
        
        [Column("Sektori")]
        [StringLength(50)]
        public string? Sektori { get; set; } // Sector
        
        [Column("Emri")]
        [StringLength(150)]
        public string? Emri { get; set; } // Name
        
        [Column("Pershkrimi")]
        [StringLength(500)]
        public string? Pershkrimi { get; set; } // Description
        
        [NotMapped]
        public string Name => Emri ?? string.Empty;
    }
    
    /// <summary>
    /// Maps to SQL Server BMDData.MetodaPagese table
    /// Albanian: MetodaPagese = Payment Method
    /// </summary>
    [Table("MetodaPagese")]
    public class MetodaPagese
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Column("Emri")]
        [StringLength(100)]
        public string? Emri { get; set; } // Name
        
        [Column("Numri")]
        [StringLength(20)]
        public string? Numri { get; set; } // Number
        
        [NotMapped]
        public string Name => Emri ?? string.Empty;
    }
    
    /// <summary>
    /// Maps to SQL Server BMDData.Tatimi table
    /// Albanian: Tatimi = Tax
    /// </summary>
    [Table("Tatimi")]
    public class Tatimi
    {
        [Key]
        [Column("ID")]
        public int Id { get; set; }
        
        [Column("Kodi")]
        [StringLength(10)]
        public string? Kodi { get; set; } // Code
        
        [Column("Emri")]
        [StringLength(100)]
        public string? Emri { get; set; } // Name
        
        [Column("Vlera")]
        public double? Vlera { get; set; } // Value (Rate)
        
        [Column("TVSH_Jo_Zbritshme")]
        public bool? TvshJoZbritshme { get; set; } // Non-deductible VAT
        
        [NotMapped]
        public string Name => Emri ?? string.Empty;
        
        [NotMapped]
        public decimal Rate => (decimal)(Vlera ?? 0);
    }
}
