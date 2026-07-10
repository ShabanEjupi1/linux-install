using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models
{
    public enum LoyaltyTier
    {
        Bronze   = 0,
        Silver   = 1,
        Gold     = 2,
        Platinum = 3
    }

    public enum LoyaltyTransactionType
    {
        Earn   = 0,
        Redeem = 1,
        Adjust = 2,
        Expire = 3
    }

    [Table("LoyaltyAccounts")]
    public class LoyaltyAccount
    {
        [Key]
        public int Id { get; set; }

        public int? CustomerId { get; set; }

        [Required, StringLength(200)]
        public string CustomerName { get; set; } = "";

        [StringLength(50)]
        public string? Phone { get; set; }

        [StringLength(100)]
        public string? Email { get; set; }

        /// <summary>Unique loyalty card number / barcode.</summary>
        [Required, StringLength(50)]
        public string CardNumber { get; set; } = "";

        public int Points { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalSpent { get; set; }

        public LoyaltyTier TierLevel { get; set; } = LoyaltyTier.Bronze;

        public DateTime JoinDate { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;
    }

    [Table("LoyaltyTransactions")]
    public class LoyaltyTransaction
    {
        [Key]
        public int Id { get; set; }

        public int LoyaltyAccountId { get; set; }

        public int Points { get; set; }

        public LoyaltyTransactionType Type { get; set; }

        public int? ReceiptId { get; set; }

        [StringLength(50)]
        public string? ReceiptNumber { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PointsMonetaryValue { get; set; }

        [StringLength(300)]
        public string? Notes { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;
    }

    [Table("LoyaltyConfig")]
    public class LoyaltyConfig
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Points earned per 1 EUR spent.</summary>
        [Column(TypeName = "decimal(10,4)")]
        public decimal PointsPerEuro { get; set; } = 1m;

        /// <summary>Monetary value of 1 point in EUR.</summary>
        [Column(TypeName = "decimal(10,4)")]
        public decimal EuroPerPoint { get; set; } = 0.01m;

        public int BronzeThreshold   { get; set; } = 0;
        public int SilverThreshold   { get; set; } = 500;
        public int GoldThreshold     { get; set; } = 2000;
        public int PlatinumThreshold { get; set; } = 5000;

        /// <summary>Maximum % of receipt total that can be paid with points.</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal MaxRedeemPercent { get; set; } = 50m;

        public bool IsEnabled { get; set; } = true;
    }
}
