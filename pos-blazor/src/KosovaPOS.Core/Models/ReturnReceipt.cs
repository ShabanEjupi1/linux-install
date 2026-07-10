using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models
{
    /// <summary>
    /// Represents a return / refund transaction referencing an original receipt.
    /// Partial or full returns are both supported.
    /// </summary>
    [Table("ReturnReceipts")]
    public class ReturnReceipt
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Human-readable return reference number (e.g. "RET-20260526-001").</summary>
        [Required]
        [StringLength(50)]
        public string ReturnNumber { get; set; } = string.Empty;

        public DateTime ReturnDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Receipt number of the original sale (stored as string so it works regardless
        /// of whether the original receipt is in the POS DB or only in DitariD).
        /// </summary>
        [Required]
        [StringLength(50)]
        public string OriginalReceiptNumber { get; set; } = string.Empty;

        /// <summary>FK to the original Receipt row when it exists in this DB (nullable).</summary>
        public int? OriginalReceiptId { get; set; }

        /// <summary>Total euro amount refunded.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal RefundAmount { get; set; }

        /// <summary>How the refund was issued: "Cash", "Card", "Kredit".</summary>
        [StringLength(50)]
        public string RefundMethod { get; set; } = "Para në dorë";

        /// <summary>Free-text reason provided by the manager.</summary>
        [StringLength(500)]
        public string? Reason { get; set; }

        /// <summary>Cashier / manager who processed the return.</summary>
        [StringLength(100)]
        public string ProcessedBy { get; set; } = string.Empty;

        /// <summary>True when a fiscal storno receipt was sent to the printer.</summary>
        public bool IsFiscalStornoPrinted { get; set; } = false;

        /// <summary>Path to the generated storno Fatura.inp file (for audit).</summary>
        [StringLength(500)]
        public string? FiscalStornoFilePath { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public List<ReturnReceiptItem> Items { get; set; } = new List<ReturnReceiptItem>();
    }

    /// <summary>Individual line item within a return.</summary>
    [Table("ReturnReceiptItems")]
    public class ReturnReceiptItem
    {
        [Key]
        public int Id { get; set; }

        public int ReturnReceiptId { get; set; }

        [Required]
        [StringLength(50)]
        public string Barcode { get; set; } = string.Empty;

        [Required]
        [StringLength(250)]
        public string ArticleName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,4)")]
        public decimal Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalValue { get; set; }

        public decimal VATRate { get; set; } = 18;

        public int? ArticleId { get; set; }

        public ReturnReceipt? ReturnReceipt { get; set; }
    }
}
