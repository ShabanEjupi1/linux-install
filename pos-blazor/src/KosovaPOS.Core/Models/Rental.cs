using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models
{
    /// <summary>Physical condition state of a rental item.</summary>
    public enum RentalItemStatus
    {
        Available  = 0,
        Rented     = 1,
        Maintenance = 2,
        Retired    = 3,
    }

    /// <summary>
    /// Represents a physical item available for rent (tool, vehicle, equipment, etc.).
    /// Linked optionally to an Artikujt row for pricing/cataloguing.
    /// </summary>
    [Table("RentalItems")]
    public class RentalItem
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Optional link to Artikujt.ArtID for price/barcode lookup.</summary>
        public int? ArticleId { get; set; }

        [Required]
        [StringLength(250)]
        public string Name { get; set; } = "";

        [StringLength(100)]
        public string? SerialNumber { get; set; }

        [StringLength(100)]
        public string? Category { get; set; }

        /// <summary>Physical condition description (e.g. "E mirë", "Dëmtuar").</summary>
        [StringLength(100)]
        public string Condition { get; set; } = "E mirë";

        [Column(TypeName = "decimal(18,2)")]
        public decimal DailyRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? HourlyRate { get; set; }

        /// <summary>Security deposit held when item is rented.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Deposit { get; set; }

        public RentalItemStatus Status { get; set; } = RentalItemStatus.Available;

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(200)]
        public string? Location { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ── Display helpers ───────────────────────────────────────────────────────

        [NotMapped]
        public string StatusDisplay => Status switch
        {
            RentalItemStatus.Available   => "E lirë",
            RentalItemStatus.Rented      => "E dhënë me qira",
            RentalItemStatus.Maintenance => "Mirëmbajtje",
            RentalItemStatus.Retired     => "Jashtë shërbimit",
            _                            => Status.ToString(),
        };

        [NotMapped]
        public string RateDisplay =>
            HourlyRate.HasValue
                ? $"€{DailyRate:F2}/ditë | €{HourlyRate:F2}/orë"
                : $"€{DailyRate:F2}/ditë";
    }

    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Status of a rental agreement.</summary>
    public enum RentalStatus
    {
        Active    = 0,
        Returned  = 1,
        Overdue   = 2,
        Cancelled = 3,
    }

    /// <summary>
    /// Rental agreement linking a customer to a RentalItem for a date range.
    /// Charges are calculated automatically on return based on actual duration.
    /// </summary>
    [Table("RentalAgreements")]
    public class RentalAgreement
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string AgreementNumber { get; set; } = "";

        public int RentalItemId { get; set; }

        [StringLength(250)]
        public string RentalItemName { get; set; } = "";

        public int? CustomerId { get; set; }

        [Required]
        [StringLength(200)]
        public string CustomerName { get; set; } = "";

        [StringLength(50)]
        public string? CustomerPhone { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime ExpectedReturnDate { get; set; }

        public DateTime? ActualReturnDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DailyRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal HourlyRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Deposit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCharged { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DepositReturned { get; set; }

        public RentalStatus Status { get; set; } = RentalStatus.Active;

        public int? ReceiptId { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(100)]
        public string CreatedBy { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ── Computed helpers ──────────────────────────────────────────────────────

        [NotMapped]
        public bool IsOverdue =>
            Status == RentalStatus.Active &&
            DateTime.Now > ExpectedReturnDate;

        [NotMapped]
        public int DaysRented =>
            (int)Math.Ceiling(
                ((ActualReturnDate ?? DateTime.Now) - StartDate).TotalDays);

        [NotMapped]
        public string StatusDisplay => Status switch
        {
            RentalStatus.Active    => IsOverdue ? "⚠️ Vonuar" : "Aktiv",
            RentalStatus.Returned  => "Kthyer",
            RentalStatus.Overdue   => "Vonuar",
            RentalStatus.Cancelled => "Anuluar",
            _                      => Status.ToString(),
        };

        [NotMapped]
        public decimal Balance => TotalCharged - AmountPaid;
    }
}
