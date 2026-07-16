using System;
using System.ComponentModel.DataAnnotations;

namespace KosovaPOS.Models
{
    /// <summary>
    /// Represents a customer in the loyalty program
    /// </summary>
    public class Customer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(20)]
        public string? Phone { get; set; }

        [StringLength(100)]
        public string? Email { get; set; }

        /// <summary>
        /// Customer loyalty card number
        /// </summary>
        [StringLength(50)]
        public string? LoyaltyCardNumber { get; set; }

        /// <summary>
        /// Current loyalty points balance
        /// </summary>
        public int LoyaltyPoints { get; set; } = 0;

        /// <summary>
        /// Total lifetime points earned
        /// </summary>
        public int TotalPointsEarned { get; set; } = 0;

        /// <summary>
        /// Total lifetime points redeemed
        /// </summary>
        public int TotalPointsRedeemed { get; set; } = 0;

        /// <summary>
        /// Customer tier: Bronze, Silver, Gold, Platinum
        /// </summary>
        [StringLength(20)]
        public string Tier { get; set; } = "Bronze";

        /// <summary>
        /// Total amount spent by the customer
        /// </summary>
        public decimal TotalSpent { get; set; } = 0;

        /// <summary>
        /// Store credit / wallet balance that can be applied at checkout
        /// </summary>
        public decimal StoreCredit { get; set; } = 0;

        /// <summary>
        /// Number of orders/visits
        /// </summary>
        public int OrderCount { get; set; } = 0;

        /// <summary>
        /// Customer's birthday for special offers
        /// </summary>
        public DateTime? Birthday { get; set; }

        /// <summary>
        /// Preferred contact method: SMS, Email, Phone
        /// </summary>
        [StringLength(20)]
        public string? PreferredContact { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        /// <summary>
        /// Customer preferences (JSON)
        /// </summary>
        public string? Preferences { get; set; }

        /// <summary>
        /// Opt-in for marketing communications
        /// </summary>
        public bool MarketingOptIn { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public DateTime? LastVisit { get; set; }
    }

    /// <summary>
    /// Loyalty points transactions
    /// </summary>
    public class LoyaltyTransaction
    {
        [Key]
        public int Id { get; set; }

        public int CustomerId { get; set; }

        [StringLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        /// <summary>
        /// Points earned (positive) or redeemed (negative)
        /// </summary>
        public int Points { get; set; }

        /// <summary>
        /// Transaction type: Earned, Redeemed, Bonus, Expired, Adjusted
        /// </summary>
        [StringLength(20)]
        public string TransactionType { get; set; } = "Earned";

        /// <summary>
        /// Reference to receipt or promotion
        /// </summary>
        [StringLength(50)]
        public string? ReferenceNumber { get; set; }

        /// <summary>
        /// Amount that generated these points (for earned) or saved (for redeemed)
        /// </summary>
        public decimal Amount { get; set; } = 0;

        /// <summary>
        /// Amount that generated these points (for earned)
        /// </summary>
        public decimal? PurchaseAmount { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.Now;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        public virtual Customer? Customer { get; set; }
    }

    /// <summary>
    /// Marketing campaigns and promotions
    /// </summary>
    public class Campaign
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// Campaign type: Discount, BonusPoints, FreeItem, BuyOneGetOne
        /// </summary>
        [StringLength(50)]
        public string CampaignType { get; set; } = "Discount";

        /// <summary>
        /// Points required to use this campaign
        /// </summary>
        public int PointsRequired { get; set; } = 0;

        /// <summary>
        /// Discount amount (percentage)
        /// </summary>
        public decimal DiscountAmount { get; set; } = 0;

        /// <summary>
        /// Discount percentage or bonus points
        /// </summary>
        public decimal Value { get; set; } = 0;

        /// <summary>
        /// Target customer tier (null = all)
        /// </summary>
        [StringLength(20)]
        public string? TargetTier { get; set; }

        /// <summary>
        /// Minimum purchase amount to qualify
        /// </summary>
        public decimal? MinimumPurchase { get; set; }

        /// <summary>
        /// Applicable article IDs (JSON array)
        /// </summary>
        public string? ApplicableArticles { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Is this campaign active?
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Status: Draft, Active, Paused, Completed
        /// </summary>
        [StringLength(20)]
        public string Status { get; set; } = "Draft";

        /// <summary>
        /// Number of times campaign was used
        /// </summary>
        public int UsageCount { get; set; } = 0;

        /// <summary>
        /// Total revenue generated from campaign
        /// </summary>
        public decimal TotalRevenue { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Track campaign usage by customers
    /// </summary>
    public class CampaignUsage
    {
        [Key]
        public int Id { get; set; }

        public int CampaignId { get; set; }
        public int CustomerId { get; set; }

        /// <summary>
        /// Reference to receipt
        /// </summary>
        public int? ReceiptId { get; set; }

        /// <summary>
        /// Discount or benefit amount
        /// </summary>
        public decimal BenefitAmount { get; set; }

        public DateTime UsedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual Campaign? Campaign { get; set; }
        public virtual Customer? Customer { get; set; }
    }

    /// <summary>
    /// Customer order history for analytics
    /// </summary>
    public class CustomerOrderHistory
    {
        [Key]
        public int Id { get; set; }

        public int CustomerId { get; set; }
        public int ReceiptId { get; set; }

        public decimal OrderTotal { get; set; }
        public int PointsEarned { get; set; }

        /// <summary>
        /// Favorite items (JSON)
        /// </summary>
        public string? OrderItems { get; set; }

        public DateTime OrderDate { get; set; }

        // Navigation property
        public virtual Customer? Customer { get; set; }
    }
}
