using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models
{
    public enum DeliveryStatus
    {
        Pending    = 0,
        Assigned   = 1,
        InTransit  = 2,
        Delivered  = 3,
        Failed     = 4,
        Cancelled  = 5
    }

    [Table("DeliveryOrders")]
    public class DeliveryOrder
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Linked receipt (nullable — order can be created before checkout).</summary>
        public int? ReceiptId { get; set; }

        public int? CustomerId { get; set; }

        [Required, StringLength(200)]
        public string CustomerName { get; set; } = "";

        [Required, StringLength(500)]
        public string DeliveryAddress { get; set; } = "";

        [StringLength(50)]
        public string? CustomerPhone { get; set; }

        public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;

        /// <summary>Employee Id of the assigned driver (role: Driver).</summary>
        public int? DriverId { get; set; }

        [StringLength(200)]
        public string? DriverName { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OrderTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? DeliveryFee { get; set; }

        public DateTime? EstimatedDelivery { get; set; }
        public DateTime? ActualDelivery { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        /// <summary>Printed delivery note has been generated.</summary>
        public bool DeliveryNotePrinted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
