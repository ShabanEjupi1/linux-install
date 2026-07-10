using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models
{
    public enum ShiftStatus
    {
        Open = 0,
        Closed = 1,
        ForceClosed = 2
    }

    /// <summary>
    /// Represents a cashier shift session. One row per opened shift.
    /// CashRegisterWindow is blocked until an Open shift exists for today.
    /// </summary>
    [Table("CashShifts")]
    public class CashShift
    {
        [Key]
        public int Id { get; set; }

        public DateTime OpenedAt { get; set; } = DateTime.Now;

        public DateTime? ClosedAt { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OpeningCash { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ClosingCash { get; set; }

        /// <summary>Expected closing cash = OpeningCash + total cash sales.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal ExpectedCash { get; set; }

        /// <summary>Difference between ExpectedCash and ClosingCash.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal CashDifference { get; set; }

        /// <summary>ID of the POSUser who opened the shift.</summary>
        public int? CashierId { get; set; }

        [StringLength(200)]
        public string? CashierName { get; set; }

        public ShiftStatus Status { get; set; } = ShiftStatus.Open;

        // Summary fields populated on close
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalSales { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCashSales { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCardSales { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalDiscounts { get; set; }

        public int TransactionCount { get; set; }

        public int ItemsSold { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        /// <summary>Serialized denomination counts entered during close (JSON).</summary>
        [StringLength(2000)]
        public string? DenominationCountJson { get; set; }
    }
}
