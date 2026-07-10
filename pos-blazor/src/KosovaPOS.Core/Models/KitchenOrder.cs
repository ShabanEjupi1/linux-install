using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models
{
    public enum KitchenOrderStatus
    {
        New        = 0,
        InProgress = 1,
        Ready      = 2,
        Served     = 3,
        Cancelled  = 4
    }

    [Table("KitchenOrders")]
    public class KitchenOrder
    {
        [Key]
        public int Id { get; set; }

        public int? ReceiptId { get; set; }

        [StringLength(50)]
        public string? ReceiptNumber { get; set; }

        [StringLength(20)]
        public string? TableNumber { get; set; }

        /// <summary>JSON-serialised list of KitchenOrderItem objects.</summary>
        public string ItemsJson { get; set; } = "[]";

        public KitchenOrderStatus Status { get; set; } = KitchenOrderStatus.New;

        public DateTime SentAt { get; set; } = DateTime.Now;

        public DateTime? ReadyAt { get; set; }

        public DateTime? ServedAt { get; set; }

        [StringLength(100)]
        public string? WaiterName { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
