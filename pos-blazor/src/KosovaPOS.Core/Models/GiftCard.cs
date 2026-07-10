using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models
{
    public enum GiftCardStatus
    {
        Active    = 0,
        Redeemed  = 1,   // fully used
        Expired   = 2,
        Cancelled = 3
    }

    [Table("GiftCards")]
    public class GiftCard
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Unique barcode / scan code on the physical card.</summary>
        [Required, StringLength(50)]
        public string Code { get; set; } = "";

        [Column(TypeName = "decimal(18,2)")]
        public decimal InitialBalance { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingBalance { get; set; }

        public GiftCardStatus Status { get; set; } = GiftCardStatus.Active;

        public int? CustomerId { get; set; }

        [StringLength(200)]
        public string? CustomerName { get; set; }

        public DateTime IssuedDate { get; set; } = DateTime.Now;
        public DateTime? ExpiryDate { get; set; }

        /// <summary>Receipt that issued this card (IssuedReceiptId).</summary>
        public int? IssuedReceiptId { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    [Table("GiftCardTransactions")]
    public class GiftCardTransaction
    {
        [Key]
        public int Id { get; set; }

        public int GiftCardId { get; set; }

        /// <summary>Issue | Redeem | Adjust | Expire</summary>
        [Required, StringLength(20)]
        public string TransactionType { get; set; } = "Redeem";

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceBefore { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceAfter { get; set; }

        public int? ReceiptId { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.Now;
    }
}
