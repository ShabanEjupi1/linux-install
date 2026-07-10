using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.BMDData
{
    /// <summary>
    /// Maps to SQL Server BMDData.Punetoret table
    /// Albanian: Punetoret = Workers/Employees
    /// </summary>
    [Table("Punetoret")]
    public class Punetoret
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Column("EmriMbiemri")]
        [StringLength(200)]
        public string? EmriMbiemri { get; set; } // Full Name
        
        [Column("Filiala")]
        public int? Filiala { get; set; } // Branch ID
        
        [Column("Shifra")]
        [StringLength(50)]
        public string? Shifra { get; set; } // Code/Password
        
        [Column("Niveli")]
        [StringLength(50)]
        public string? Niveli { get; set; } // Access Level (Admin, Manager, Cashier)
        
        [Column("Data")]
        [StringLength(20)]
        public string? Data { get; set; } // Date
        
        [Column("Pershkrimi")]
        [StringLength(500)]
        public string? Pershkrimi { get; set; } // Description
        
        // Computed properties for compatibility with User model
        [NotMapped]
        public string FullName => EmriMbiemri ?? string.Empty;
        
        [NotMapped]
        public string Username => Shifra ?? Id.ToString();
        
        [NotMapped]
        public string Role => Niveli ?? "Cashier";
        
        [NotMapped]
        public bool IsActive => true;
    }
}
