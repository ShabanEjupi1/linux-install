using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace KosovaPOS.Models
{
    /// <summary>
    /// Represents a specific size/color variant of a parent article.
    /// Each variant has its own barcode and optionally overrides the parent price.
    /// </summary>
    [Table("ArticleVariants")]
    public class ArticleVariant
    {
        [Key]
        public int Id { get; set; }

        /// <summary>ID in the Artikujt table (parent article).</summary>
        public int ParentArticleId { get; set; }

        [StringLength(50)]
        public string? ParentBarcode { get; set; }

        [StringLength(250)]
        public string ParentName { get; set; } = "";

        /// <summary>Unique barcode for this variant (scanned at register).</summary>
        [Required]
        [StringLength(50)]
        public string Barcode { get; set; } = "";

        [StringLength(50)]
        public string? Size { get; set; }

        [StringLength(50)]
        public string? Color { get; set; }

        /// <summary>Material or sub-type description.</summary>
        [StringLength(100)]
        public string? Material { get; set; }

        /// <summary>Current stock quantity for this variant.</summary>
        public decimal StockQuantity { get; set; }

        /// <summary>When set, overrides the parent article's sales price for this variant.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? SalesPriceOverride { get; set; }

        /// <summary>Fiscal printer PLU number (if different from parent).</summary>
        public int? PLU { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // ── Computed helpers (not mapped to DB) ──────────────────────────────────

        [NotMapped]
        public string DisplayLabel =>
            string.Join(" / ", new[] { Size, Color, Material }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
    }
}
