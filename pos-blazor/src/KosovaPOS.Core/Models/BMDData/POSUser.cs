using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.BMDData
{
    /// <summary>
    /// Maps to SQL Server BMDData.POSUsers table
    /// User model for authentication and role-based access control
    /// </summary>
    [Table("POSUsers")]
    public class POSUser
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Required]
        [Column("Username")]
        [StringLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [Column("PasswordHash")]
        [StringLength(256)]
        public string PasswordHash { get; set; } = string.Empty; // BCrypt hashed

        [Column("Email")]
        [StringLength(100)]
        public string? Email { get; set; }

        [Required]
        [Column("FullName")]
        [StringLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Column("Role")]
        [StringLength(50)]
        public string Role { get; set; } = "Cashier"; // Admin, Manager, Cashier, Warehouse, Accountant

        [Column("IsActive")]
        public bool IsActive { get; set; } = true;

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("LastLogin")]
        public DateTime? LastLogin { get; set; }

        [Column("Branch")]
        [StringLength(200)]
        public string? Branch { get; set; }

        [Column("PhoneNumber")]
        [StringLength(50)]
        public string? PhoneNumber { get; set; }

        // Permissions
        [Column("CanManageArticles")]
        public bool CanManageArticles { get; set; } = true;
        
        [Column("CanManagePurchases")]
        public bool CanManagePurchases { get; set; } = false;
        
        [Column("CanManageUsers")]
        public bool CanManageUsers { get; set; } = false;
        
        [Column("CanViewReports")]
        public bool CanViewReports { get; set; } = true;
        
        [Column("CanModifyPrices")]
        public bool CanModifyPrices { get; set; } = false;
        
        [Column("CanDeleteReceipts")]
        public bool CanDeleteReceipts { get; set; } = false;
        
        [Column("CanGiveDiscounts")]
        public bool CanGiveDiscounts { get; set; } = false;

        /// <summary>Ring up sales: the till, receipts, returns and the cash register.</summary>
        [Column("CanSell")]
        public bool CanSell { get; set; } = true;

        /// <summary>Stock on hand, inventory adjustments and barcode label printing.</summary>
        [Column("CanManageStock")]
        public bool CanManageStock { get; set; } = false;

        [Column("MaxDiscountPercent")]
        public decimal MaxDiscountPercent { get; set; } = 0;
        
        /// <summary>
        /// Convert to User model for compatibility
        /// </summary>
        public User ToUser()
        {
            return new User
            {
                Id = Id,
                Username = Username,
                PasswordHash = PasswordHash,
                Email = Email,
                FullName = FullName,
                Role = Role,
                IsActive = IsActive,
                CreatedAt = CreatedAt,
                LastLogin = LastLogin,
                Branch = Branch,
                PhoneNumber = PhoneNumber,
                CanManageArticles = CanManageArticles,
                CanManagePurchases = CanManagePurchases,
                CanManageUsers = CanManageUsers,
                CanViewReports = CanViewReports,
                CanModifyPrices = CanModifyPrices,
                CanDeleteReceipts = CanDeleteReceipts,
                CanGiveDiscounts = CanGiveDiscounts,
                MaxDiscountPercent = MaxDiscountPercent
            };
        }
    }
}
