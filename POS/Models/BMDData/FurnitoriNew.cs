using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.BMDData
{
    /// <summary>
    /// Maps to SQL Server BMDData.FurnitoriNew table
    /// Albanian: Furnitori = Supplier, also used for Customers
    /// </summary>
    [Table("FurnitoriNew")]
    public class FurnitoriNew
    {
        [Key]
        [Column("Id")]
        public long Id { get; set; }
        
        [Column("Emri")]
        [StringLength(200)]
        public string? Emri { get; set; } // Name
        
        [Column("NRF")]
        [StringLength(50)]
        public string? NRF { get; set; } // Fiscal Registration Number
        
        [Column("NIT")]
        [StringLength(50)]
        public string? NIT { get; set; } // Unique Identification Number (NUI)
        
        [Column("Personi")]
        [StringLength(200)]
        public string? Personi { get; set; } // Contact Person
        
        [Column("Adresa")]
        [StringLength(300)]
        public string? Adresa { get; set; } // Address
        
        [Column("Qyteti")]
        public int? Qyteti { get; set; } // City ID
        
        [Column("Telefoni")]
        [StringLength(50)]
        public string? Telefoni { get; set; } // Phone
        
        [Column("Xhirollogaria")]
        [StringLength(50)]
        public string? Xhirollogaria { get; set; } // Bank Account
        
        [Column("Email")]
        [StringLength(100)]
        public string? Email { get; set; } // Email
        
        [Column("F")]
        public bool? F { get; set; } // Is Supplier
        
        [Column("K")]
        public bool? K { get; set; } // Is Customer
        
        [Column("Data")]
        [StringLength(20)]
        public string? Data { get; set; } // Date
        
        [Column("Prejashtuar_TVSH")]
        public bool? PrejashtarTvsh { get; set; } // VAT Exempt
        
        // Computed properties for compatibility with BusinessPartner model
        [NotMapped]
        public string Name => Emri ?? string.Empty;
        
        [NotMapped]
        public string? NUI => NIT;
        
        [NotMapped]
        public string? Address => Adresa;
        
        [NotMapped]
        public string? Phone => Telefoni;
        
        [NotMapped]
        public string PartnerType => 
            (F == true && K == true) ? "Të dy" :
            (F == true) ? "Furnizues" :
            (K == true) ? "Klient" : "Klient";
        
        [NotMapped]
        public bool IsActive => true;
    }
}
