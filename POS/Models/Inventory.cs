using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models
{
    /// <summary>
    /// Represents inventory stock levels for products
    /// </summary>
    public class InventoryStock
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Reference to Article (Artikujt)
        /// </summary>
        public int ArticleId { get; set; }

        [Required]
        [StringLength(100)]
        public string ArticleName { get; set; } = string.Empty;

        /// <summary>
        /// Current quantity in stock
        /// </summary>
        public decimal CurrentQuantity { get; set; } = 0;

        /// <summary>
        /// Minimum quantity threshold for low stock alert
        /// </summary>
        public decimal MinimumQuantity { get; set; } = 0;

        /// <summary>
        /// Reorder quantity - how much to order when stock is low
        /// </summary>
        public decimal ReorderQuantity { get; set; } = 0;

        /// <summary>
        /// Unit of measurement
        /// </summary>
        [StringLength(20)]
        public string Unit { get; set; } = "pcs";

        /// <summary>
        /// Last restock date
        /// </summary>
        public DateTime? LastRestockedAt { get; set; }

        /// <summary>
        /// Warehouse/location
        /// </summary>
        [StringLength(100)]
        public string? Location { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Represents inventory movements (in/out)
    /// </summary>
    public class InventoryMovement
    {
        [Key]
        public int Id { get; set; }

        public int InventoryStockId { get; set; }
        public int ArticleId { get; set; }

        /// <summary>
        /// Article name (denormalized for performance)
        /// </summary>
        [StringLength(100)]
        public string ArticleName { get; set; } = string.Empty;

        /// <summary>
        /// Movement type: StockIn, StockOut, Adjustment, Transfer, Waste, Return, In, Out
        /// </summary>
        [StringLength(20)]
        public string MovementType { get; set; } = "StockIn";

        /// <summary>
        /// Quantity (positive for in, negative for out)
        /// </summary>
        public decimal Quantity { get; set; }

        /// <summary>
        /// Unit of measurement
        /// </summary>
        [StringLength(20)]
        public string Unit { get; set; } = "pcs";

        /// <summary>
        /// Description of the movement
        /// </summary>
        [StringLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Reference document: PO number, Sales receipt, etc.
        /// </summary>
        [StringLength(50)]
        public string? Reference { get; set; }

        /// <summary>
        /// Reference document: PO number, Sales receipt, etc. (alternative name)
        /// </summary>
        [StringLength(50)]
        public string? ReferenceNumber { get; set; }

        /// <summary>
        /// Batch/Lot number
        /// </summary>
        [StringLength(50)]
        public string? BatchNumber { get; set; }

        /// <summary>
        /// Expiry date for batch
        /// </summary>
        public DateTime? ExpiryDate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime MovementDate { get; set; } = DateTime.Now;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        public virtual InventoryStock? InventoryStock { get; set; }
    }

    /// <summary>
    /// Represents suppliers for automatic reordering
    /// </summary>
    public class InventorySupplier
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Code { get; set; }

        [StringLength(100)]
        public string? ContactPerson { get; set; }

        [StringLength(20)]
        public string? Phone { get; set; }

        [StringLength(100)]
        public string? Email { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        /// <summary>
        /// Payment terms in days
        /// </summary>
        public int PaymentTerms { get; set; } = 30;

        /// <summary>
        /// Average delivery time in days
        /// </summary>
        public int LeadTimeDays { get; set; } = 7;

        /// <summary>
        /// Supplier rating (1-5 stars)
        /// </summary>
        public decimal Rating { get; set; } = 0;

        /// <summary>
        /// Current balance (amount owed to supplier)
        /// </summary>
        public decimal Balance { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Links articles to their suppliers with pricing
    /// </summary>
    public class ArticleSupplier
    {
        [Key]
        public int Id { get; set; }

        public int ArticleId { get; set; }
        public int SupplierId { get; set; }

        /// <summary>
        /// Supplier's product code
        /// </summary>
        [StringLength(50)]
        public string? SupplierProductCode { get; set; }

        /// <summary>
        /// Purchase price from this supplier
        /// </summary>
        public decimal PurchasePrice { get; set; }

        /// <summary>
        /// Minimum order quantity
        /// </summary>
        public decimal MinOrderQuantity { get; set; } = 1;

        /// <summary>
        /// Is this the preferred supplier?
        /// </summary>
        public bool IsPreferred { get; set; } = false;

        /// <summary>
        /// Lead time in days for this article from this supplier
        /// </summary>
        public int LeadTimeDays { get; set; } = 7;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual InventorySupplier? Supplier { get; set; }
    }

    /// <summary>
    /// Automatic reorder suggestions
    /// </summary>
    public class ReorderSuggestion
    {
        [Key]
        public int Id { get; set; }

        public int ArticleId { get; set; }
        public int? SupplierId { get; set; }

        [Required]
        [StringLength(100)]
        public string ArticleName { get; set; } = string.Empty;

        public decimal CurrentStock { get; set; }
        public decimal MinimumStock { get; set; }
        public decimal SuggestedOrderQuantity { get; set; }

        /// <summary>
        /// Estimated cost for this reorder
        /// </summary>
        public decimal EstimatedCost { get; set; }

        /// <summary>
        /// Status: Pending, Ordered, Cancelled
        /// </summary>
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// Reference to purchase order if created
        /// </summary>
        public int? PurchaseOrderId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation property
        public virtual InventorySupplier? Supplier { get; set; }
    }

    /// <summary>
    /// Stock alerts/notifications
    /// </summary>
    public class StockAlert
    {
        [Key]
        public int Id { get; set; }

        public int ArticleId { get; set; }

        [Required]
        [StringLength(100)]
        public string ArticleName { get; set; } = string.Empty;

        /// <summary>
        /// Alert type: LowStock, OutOfStock, Expiring, Expired
        /// </summary>
        [StringLength(20)]
        public string AlertType { get; set; } = "LowStock";

        public decimal CurrentQuantity { get; set; }
        public decimal ThresholdQuantity { get; set; }
        public decimal MinimumQuantity { get; set; }

        /// <summary>
        /// Alert message
        /// </summary>
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Expiry date if applicable
        /// </summary>
        public DateTime? ExpiryDate { get; set; }

        /// <summary>
        /// Alert status: Active, Resolved, Dismissed
        /// </summary>
        [StringLength(20)]
        public string Status { get; set; } = "Active";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ResolvedAt { get; set; }
    }
}
