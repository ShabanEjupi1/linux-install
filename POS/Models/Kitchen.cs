using System;
using System.ComponentModel.DataAnnotations;

namespace KosovaPOS.Models
{
    /// <summary>
    /// Represents a kitchen display order
    /// </summary>
    public class KitchenOrder
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Reference to receipt
        /// </summary>
        public int ReceiptId { get; set; }

        /// <summary>
        /// Order number for kitchen display
        /// </summary>
        public int OrderNumber { get; set; }

        /// <summary>
        /// Order type: DineIn, TakeOut, Delivery
        /// </summary>
        [StringLength(20)]
        public string OrderType { get; set; } = "DineIn";

        /// <summary>
        /// Table number (if dine-in)
        /// </summary>
        [StringLength(20)]
        public string? TableNumber { get; set; }

        /// <summary>
        /// Customer name (for takeout/delivery)
        /// </summary>
        [StringLength(100)]
        public string? CustomerName { get; set; }

        /// <summary>
        /// Order items (JSON)
        /// </summary>
        public string? OrderItems { get; set; }

        /// <summary>
        /// Special instructions
        /// </summary>
        [StringLength(1000)]
        public string? SpecialInstructions { get; set; }

        /// <summary>
        /// Status: New, Preparing, Ready, Served, Cancelled
        /// </summary>
        [StringLength(20)]
        public string Status { get; set; } = "New";

        /// <summary>
        /// Priority: Normal, High, Urgent
        /// </summary>
        [StringLength(20)]
        public string Priority { get; set; } = "Normal";

        /// <summary>
        /// Kitchen station: Grill, Pizza, Salad, Drinks, etc.
        /// </summary>
        [StringLength(50)]
        public string? Station { get; set; }

        /// <summary>
        /// Assigned kitchen staff
        /// </summary>
        [StringLength(100)]
        public string? AssignedTo { get; set; }

        /// <summary>
        /// Time order was received
        /// </summary>
        public DateTime ReceivedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Time preparation started
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// Time order was marked ready
        /// </summary>
        public DateTime? ReadyAt { get; set; }

        /// <summary>
        /// Time order was served/picked up
        /// </summary>
        public DateTime? ServedAt { get; set; }

        /// <summary>
        /// Target preparation time in minutes
        /// </summary>
        public int TargetTime { get; set; } = 15;

        /// <summary>
        /// Actual preparation time in minutes (calculated)
        /// </summary>
        public int? ActualTime { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Kitchen order items with preparation status
    /// </summary>
    public class KitchenOrderItem
    {
        [Key]
        public int Id { get; set; }

        public int KitchenOrderId { get; set; }

        /// <summary>
        /// Reference to article
        /// </summary>
        public int ArticleId { get; set; }

        [Required]
        [StringLength(200)]
        public string ItemName { get; set; } = string.Empty;

        public int Quantity { get; set; } = 1;

        /// <summary>
        /// Status: Pending, Preparing, Ready
        /// </summary>
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// Item modifications (JSON)
        /// </summary>
        public string? Modifications { get; set; }

        [StringLength(500)]
        public string? SpecialInstructions { get; set; }

        /// <summary>
        /// Kitchen station responsible
        /// </summary>
        [StringLength(50)]
        public string? Station { get; set; }

        /// <summary>
        /// Preparation sequence number
        /// </summary>
        public int SequenceNumber { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation property
        public virtual KitchenOrder? KitchenOrder { get; set; }
    }

    /// <summary>
    /// Ingredient preparation tracking
    /// </summary>
    public class IngredientPrep
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Ingredient/article ID
        /// </summary>
        public int IngredientId { get; set; }

        [Required]
        [StringLength(100)]
        public string IngredientName { get; set; } = string.Empty;

        /// <summary>
        /// Prep type: Chopped, Sliced, Marinated, Cooked, etc.
        /// </summary>
        [StringLength(50)]
        public string PrepType { get; set; } = "Raw";

        /// <summary>
        /// Quantity prepared
        /// </summary>
        public decimal QuantityPrepared { get; set; }

        /// <summary>
        /// Unit of measurement
        /// </summary>
        [StringLength(20)]
        public string Unit { get; set; } = "pcs";

        /// <summary>
        /// Current available quantity
        /// </summary>
        public decimal CurrentQuantity { get; set; }

        /// <summary>
        /// Minimum quantity threshold
        /// </summary>
        public decimal MinimumQuantity { get; set; } = 0;

        /// <summary>
        /// Expiry time for prepped ingredient
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Prepared by
        /// </summary>
        [StringLength(100)]
        public string? PreparedBy { get; set; }

        public DateTime PreparedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Kitchen station configuration
    /// </summary>
    public class KitchenStation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Station code for display
        /// </summary>
        [StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Articles handled by this station (JSON array of article IDs)
        /// </summary>
        public string? HandledArticles { get; set; }

        /// <summary>
        /// Average prep time in minutes
        /// </summary>
        public int AveragePrepTime { get; set; } = 10;

        /// <summary>
        /// Maximum concurrent orders
        /// </summary>
        public int MaxConcurrentOrders { get; set; } = 5;

        /// <summary>
        /// Display color for UI
        /// </summary>
        [StringLength(20)]
        public string? DisplayColor { get; set; }

        /// <summary>
        /// Display order/sequence
        /// </summary>
        public int DisplayOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Kitchen performance metrics
    /// </summary>
    public class KitchenPerformance
    {
        [Key]
        public int Id { get; set; }

        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        /// <summary>
        /// Station or null for overall
        /// </summary>
        [StringLength(100)]
        public string? Station { get; set; }

        /// <summary>
        /// Total orders processed
        /// </summary>
        public int TotalOrders { get; set; } = 0;

        /// <summary>
        /// Orders completed on time
        /// </summary>
        public int OrdersOnTime { get; set; } = 0;

        /// <summary>
        /// Orders completed late
        /// </summary>
        public int OrdersLate { get; set; } = 0;

        /// <summary>
        /// Average preparation time in minutes
        /// </summary>
        public decimal AveragePrepTime { get; set; } = 0;

        /// <summary>
        /// Peak prep time (slowest order)
        /// </summary>
        public decimal PeakPrepTime { get; set; } = 0;

        /// <summary>
        /// Fastest prep time
        /// </summary>
        public decimal FastestPrepTime { get; set; } = 0;

        /// <summary>
        /// Orders cancelled/voided
        /// </summary>
        public int OrdersCancelled { get; set; } = 0;

        /// <summary>
        /// On-time percentage
        /// </summary>
        public decimal OnTimePercentage { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
