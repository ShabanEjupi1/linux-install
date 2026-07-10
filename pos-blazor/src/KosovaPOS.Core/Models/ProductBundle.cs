using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models
{
    /// <summary>
    /// Defines a bundle (BOM) relationship: one parent article is composed of
    /// one or more component articles. Multiple rows share the same ParentArticleId.
    /// </summary>
    [Table("ProductBundles")]
    public class ProductBundle
    {
        [Key]
        public int Id { get; set; }

        /// <summary>The "parent" bundle article (e.g., "Gift Set A").</summary>
        public int ParentArticleId { get; set; }

        [StringLength(50)]
        public string ParentBarcode { get; set; } = "";

        [StringLength(250)]
        public string ParentName { get; set; } = "";

        /// <summary>One component of the bundle.</summary>
        public int ComponentArticleId { get; set; }

        [StringLength(50)]
        public string ComponentBarcode { get; set; } = "";

        [StringLength(250)]
        public string ComponentName { get; set; } = "";

        /// <summary>How many units of this component are included per bundle unit.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal Quantity { get; set; } = 1;

        /// <summary>
        /// When true the bundle line is replaced in the cart by its individual components.
        /// When false the bundle is kept as a single line and components are hidden.
        /// </summary>
        public bool AutoExpandOnSale { get; set; }

        /// <summary>Deduct component stock quantities on sale (instead of parent stock).</summary>
        public bool DeductComponentStock { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
