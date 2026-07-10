using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models
{
    /// <summary>Types of automatic pricing rules.</summary>
    public enum PriceRuleType
    {
        /// <summary>Discount applied when quantity meets a minimum threshold.</summary>
        QuantityBreak = 0,
        /// <summary>Discount based on customer loyalty tier.</summary>
        CustomerTier = 1,
        /// <summary>Discount active only on specific days of the week.</summary>
        DayOfWeek = 2,
        /// <summary>Discount active only within a specific time window each day.</summary>
        TimeRange = 3,
        /// <summary>Buy X items, get Y items free or at reduced price.</summary>
        BuyXGetY = 4,
    }

    /// <summary>
    /// Defines an automatic pricing rule evaluated in CashRegisterWindow on every
    /// line-item change. Multiple rules can stack; Priority controls evaluation order
    /// (lower value = higher priority). Only the first matching rule per line is applied
    /// unless IsStackable is true.
    /// </summary>
    [Table("PriceRules")]
    public class PriceRule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = "";

        [StringLength(500)]
        public string? Description { get; set; }

        public PriceRuleType Type { get; set; } = PriceRuleType.QuantityBreak;

        // ── Article / Category scope ──────────────────────────────────────────────

        /// <summary>Null = rule applies to all articles.</summary>
        public int? ArticleId { get; set; }

        [StringLength(50)]
        public string? ArticleBarcode { get; set; }

        [StringLength(150)]
        public string? CategoryName { get; set; }

        // ── Discount values (at least one must be set) ───────────────────────────

        [Column(TypeName = "decimal(8,4)")]
        public decimal? DiscountPercent { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? DiscountAmount { get; set; }

        // ── QuantityBreak ─────────────────────────────────────────────────────────

        [Column(TypeName = "decimal(18,4)")]
        public decimal MinQuantity { get; set; } = 1;

        // ── BuyXGetY ──────────────────────────────────────────────────────────────

        /// <summary>Quantity customer must buy (X in Buy-X-Get-Y).</summary>
        public int? BuyQuantity { get; set; }

        /// <summary>Quantity given free or discounted (Y in Buy-X-Get-Y).</summary>
        public int? GetQuantity { get; set; }

        // ── DayOfWeek (0=Sunday … 6=Saturday, or -1 for all) ─────────────────────

        public int DayOfWeekFilter { get; set; } = -1;

        // ── TimeRange ─────────────────────────────────────────────────────────────

        public TimeSpan? TimeRangeStart { get; set; }
        public TimeSpan? TimeRangeEnd   { get; set; }

        // ── Date range validity ────────────────────────────────────────────────────

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate   { get; set; }

        // ── Configuration ─────────────────────────────────────────────────────────

        /// <summary>Lower value = evaluated first when multiple rules match.</summary>
        public int Priority { get; set; } = 10;

        /// <summary>True to allow this rule to stack with other matching rules.</summary>
        public bool IsStackable { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ── Display helpers ───────────────────────────────────────────────────────

        [NotMapped]
        public string DiscountDisplay =>
            DiscountPercent.HasValue
                ? $"{DiscountPercent:F1}%"
                : DiscountAmount.HasValue
                    ? $"€{DiscountAmount:F2}"
                    : "";

        [NotMapped]
        public string ScopeDisplay =>
            !string.IsNullOrEmpty(ArticleBarcode)
                ? $"Art: {ArticleBarcode}"
                : !string.IsNullOrEmpty(CategoryName)
                    ? $"Kat: {CategoryName}"
                    : "Të gjitha";

        [NotMapped]
        public string StatusDisplay => IsActive ? "Aktiv" : "Joaktiv";
    }
}
