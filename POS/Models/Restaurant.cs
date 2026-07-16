using System;
using System.ComponentModel.DataAnnotations;

namespace KosovaPOS.Models
{
    /// <summary>
    /// Represents a table in the restaurant
    /// </summary>
    public class RestaurantTable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string TableNumber { get; set; } = string.Empty;

        /// <summary>
        /// Seating capacity
        /// </summary>
        public int Capacity { get; set; } = 4;

        /// <summary>
        /// Table location/area: Indoor, Outdoor, VIP, etc.
        /// </summary>
        [StringLength(50)]
        public string? Location { get; set; }

        /// <summary>
        /// Table status: Available, Occupied, Reserved, Cleaning
        /// </summary>
        [StringLength(20)]
        public string Status { get; set; } = "Available";

        /// <summary>
        /// Current receipt ID if occupied
        /// </summary>
        public int? CurrentReceiptId { get; set; }

        /// <summary>
        /// Time when table was occupied
        /// </summary>
        public DateTime? OccupiedSince { get; set; }

        /// <summary>
        /// Reserved for (customer name)
        /// </summary>
        [StringLength(100)]
        public string? ReservedFor { get; set; }

        /// <summary>
        /// Reserved until (date/time)
        /// </summary>
        public DateTime? ReservedUntil { get; set; }

        /// <summary>
        /// Reserved phone number
        /// </summary>
        [StringLength(20)]
        public string? ReservedPhone { get; set; }

        /// <summary>
        /// Is this table active/enabled?
        /// </summary>
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation property to current receipt
        public virtual Receipt? CurrentReceipt { get; set; }
    }
    
    /// <summary>
    /// Represents table reservations
    /// </summary>
    public class TableReservation
    {
        [Key]
        public int Id { get; set; }
        
        public int TableId { get; set; }
        
        [Required]
        [StringLength(100)]
        public string CustomerName { get; set; } = string.Empty;
        
        [StringLength(20)]
        public string? CustomerPhone { get; set; }
        
        public DateTime ReservationDate { get; set; }
        
        public DateTime ReservationTime { get; set; }
        
        /// <summary>
        /// Number of guests
        /// </summary>
        public int GuestCount { get; set; } = 1;
        
        /// <summary>
        /// Reservation status: Confirmed, Pending, Cancelled, Completed
        /// </summary>
        [StringLength(20)]
        public string Status { get; set; } = "Pending";
        
        [StringLength(500)]
        public string? Notes { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        
        // Navigation property
        public virtual RestaurantTable? Table { get; set; }
    }
    
    /// <summary>
    /// Represents a delivery order
    /// </summary>
    public class DeliveryOrder
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Human-readable order number, e.g. DEL-20240101-143022
        /// </summary>
        [Required]
        [StringLength(50)]
        public string OrderNumber { get; set; } = string.Empty;

        /// <summary>
        /// Reference to Receipt
        /// </summary>
        public int ReceiptId { get; set; }
        
        [Required]
        [StringLength(100)]
        public string CustomerName { get; set; } = string.Empty;
        
        [Required]
        [StringLength(20)]
        public string CustomerPhone { get; set; } = string.Empty;
        
        [Required]
        [StringLength(500)]
        public string DeliveryAddress { get; set; } = string.Empty;
        
        /// <summary>
        /// Total order amount
        /// </summary>
        public decimal TotalAmount { get; set; } = 0;

        /// <summary>
        /// Delivery fee
        /// </summary>
        public decimal DeliveryFee { get; set; } = 0;

        /// <summary>
        /// Estimated delivery time in minutes (stored in EstDeliveryMinutes column)
        /// </summary>
        public int EstimatedDeliveryTime { get; set; } = 30;
        
        /// <summary>
        /// Order status: Pending, Preparing, Ready, OnTheWay, Delivered, Cancelled
        /// </summary>
        [StringLength(20)]
        public string Status { get; set; } = "Pending";
        
        /// <summary>
        /// Delivery driver ID (if assigned)
        /// </summary>
        public int? DriverId { get; set; }
        
        [StringLength(100)]
        public string? DriverName { get; set; }
        
        /// <summary>
        /// Time when order was accepted
        /// </summary>
        public DateTime? AcceptedAt { get; set; }
        
        /// <summary>
        /// Time when driver picked up the order
        /// </summary>
        public DateTime? PickedUpAt { get; set; }
        
        /// <summary>
        /// Time when order was delivered
        /// </summary>
        public DateTime? DeliveredAt { get; set; }
        
        [StringLength(1000)]
        public string? Notes { get; set; }
        
        /// <summary>
        /// Customer rating (1-5 stars)
        /// </summary>
        public int? Rating { get; set; }
        
        [StringLength(500)]
        public string? CustomerFeedback { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
    
    /// <summary>
    /// Represents a delivery driver
    /// </summary>
    public class DeliveryDriver
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;
        
        [StringLength(50)]
        public string? VehicleType { get; set; }
        
        [StringLength(20)]
        public string? VehiclePlate { get; set; }
        
        /// <summary>
        /// Driver status: Available, Busy, Offline
        /// </summary>
        [StringLength(20)]
        public string Status { get; set; } = "Available";
        
        /// <summary>
        /// Current number of active deliveries
        /// </summary>
        public int ActiveDeliveries { get; set; } = 0;
        
        /// <summary>
        /// Total completed deliveries
        /// </summary>
        public int TotalDeliveries { get; set; } = 0;
        
        /// <summary>
        /// Average rating
        /// </summary>
        public decimal AverageRating { get; set; } = 0;
        
        /// <summary>
        /// Is driver currently active/enabled?
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
    
    /// <summary>
    /// Represents online orders from web/mobile app
    /// </summary>
    public class OnlineOrder
    {
        [Key]
        public int Id { get; set; }
        
        /// <summary>
        /// Unique order number for customer
        /// </summary>
        [Required]
        [StringLength(50)]
        public string OrderNumber { get; set; } = string.Empty;
        
        /// <summary>
        /// Reference to Receipt (when processed)
        /// </summary>
        public int? ReceiptId { get; set; }
        
        /// <summary>
        /// Order type: Delivery, Pickup
        /// </summary>
        [StringLength(20)]
        public string OrderType { get; set; } = "Delivery";
        
        [Required]
        [StringLength(100)]
        public string CustomerName { get; set; } = string.Empty;
        
        [StringLength(100)]
        public string? CustomerEmail { get; set; }
        
        [Required]
        [StringLength(20)]
        public string CustomerPhone { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string? DeliveryAddress { get; set; }
        
        /// <summary>
        /// Payment method: Cash, Card, Online
        /// </summary>
        [StringLength(20)]
        public string PaymentMethod { get; set; } = "Cash";
        
        /// <summary>
        /// Payment status: Pending, Paid, Failed
        /// </summary>
        [StringLength(20)]
        public string PaymentStatus { get; set; } = "Pending";
        
        /// <summary>
        /// Order items as JSON
        /// </summary>
        public string? OrderItemsJson { get; set; }
        
        /// <summary>
        /// Subtotal amount
        /// </summary>
        public decimal Subtotal { get; set; }
        
        /// <summary>
        /// Delivery fee (if applicable)
        /// </summary>
        public decimal DeliveryFee { get; set; } = 0;
        
        /// <summary>
        /// Discount amount
        /// </summary>
        public decimal Discount { get; set; } = 0;
        
        /// <summary>
        /// Total amount
        /// </summary>
        public decimal Total { get; set; }
        
        /// <summary>
        /// Order status: New, Confirmed, Preparing, Ready, Completed, Cancelled
        /// </summary>
        [StringLength(20)]
        public string Status { get; set; } = "New";
        
        /// <summary>
        /// Scheduled pickup/delivery time
        /// </summary>
        public DateTime? ScheduledFor { get; set; }
        
        [StringLength(1000)]
        public string? SpecialInstructions { get; set; }
        
        /// <summary>
        /// Reference to delivery order
        /// </summary>
        public int? DeliveryOrderId { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
