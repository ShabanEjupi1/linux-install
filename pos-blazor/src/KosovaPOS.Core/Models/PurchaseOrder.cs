using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models
{
    /// <summary>Status lifecycle of a Purchase Order.</summary>
    public enum POStatus
    {
        Draft             = 0,
        Sent              = 1,
        PartiallyReceived = 2,
        Received          = 3,
        Cancelled         = 4,
    }

    /// <summary>
    /// A supplier purchase order (PO). Tracks ordered vs received quantities
    /// and can automatically update stock when goods are received.
    /// </summary>
    [Table("PurchaseOrders")]
    public class PurchaseOrder
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string OrderNumber { get; set; } = "";

        public int? SupplierId { get; set; }

        [Required]
        [StringLength(200)]
        public string SupplierName { get; set; } = "";

        [StringLength(100)]
        public string? SupplierContact { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.Now;

        public DateTime? ExpectedDelivery { get; set; }

        public DateTime? ReceivedDate { get; set; }

        public POStatus Status { get; set; } = POStatus.Draft;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal VATAmount { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(100)]
        public string CreatedBy { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>Navigation property – populated by EF via Include.</summary>
        public List<PurchaseOrderItem> Items { get; set; } = new();

        // ── Display helpers ───────────────────────────────────────────────────────

        [NotMapped]
        public string StatusDisplay => Status switch
        {
            POStatus.Draft             => "Draft",
            POStatus.Sent              => "Dërguar",
            POStatus.PartiallyReceived => "Marrë pjesërisht",
            POStatus.Received          => "Marrë",
            POStatus.Cancelled         => "Anuluar",
            _                          => Status.ToString(),
        };

        [NotMapped]
        public string StatusIcon => Status switch
        {
            POStatus.Draft             => "📝",
            POStatus.Sent              => "📤",
            POStatus.PartiallyReceived => "📦",
            POStatus.Received          => "✅",
            POStatus.Cancelled         => "❌",
            _                          => "",
        };
    }

    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>A single line item within a Purchase Order.</summary>
    [Table("PurchaseOrderItems")]
    public class PurchaseOrderItem
    {
        [Key]
        public int Id { get; set; }

        public int PurchaseOrderId { get; set; }

        /// <summary>Foreign key to Artikujt.ArtID (nullable for free-text items).</summary>
        public int? ArticleId { get; set; }

        [StringLength(50)]
        public string? Barcode { get; set; }

        [Required]
        [StringLength(250)]
        public string ArticleName { get; set; } = "";

        [StringLength(50)]
        public string? Unit { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal OrderedQty { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal ReceivedQty { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(8,2)")]
        public decimal VATRate { get; set; } = 18;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        [StringLength(300)]
        public string? Notes { get; set; }

        // ── Computed helpers ──────────────────────────────────────────────────────

        [NotMapped]
        public decimal RemainingQty => OrderedQty - ReceivedQty;

        [NotMapped]
        public bool IsFullyReceived => ReceivedQty >= OrderedQty;
    }
}
