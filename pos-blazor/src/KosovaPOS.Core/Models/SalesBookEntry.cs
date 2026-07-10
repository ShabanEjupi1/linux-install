using System;
using System.ComponentModel.DataAnnotations;

namespace KosovaPOS.Models
{
    /// <summary>
    /// Represents one invoice row in the Book of Sales (Libri i Shitjeve).
    /// Required by Kosovo law / ATK for monthly VAT declarations.
    /// Each row = one fiscal or non-fiscal sales invoice issued.
    /// </summary>
    public class SalesBookEntry
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Invoice / receipt date.</summary>
        public DateTime InvoiceDate { get; set; }

        /// <summary>Sequential invoice / receipt number.</summary>
        [Required]
        [StringLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty;

        /// <summary>Buyer name (or "Qytetar" for end consumers).</summary>
        [StringLength(200)]
        public string BuyerName { get; set; } = "Qytetar";

        /// <summary>Buyer NUI / ID number (empty for retail consumers).</summary>
        [StringLength(50)]
        public string? BuyerNUI { get; set; }

        /// <summary>Buyer fiscal number (empty for retail consumers).</summary>
        [StringLength(50)]
        public string? BuyerFiscalNumber { get; set; }

        // ─── VAT Breakdown ───────────────────────────────────────────────────────────

        /// <summary>Net amount subject to 18% VAT.</summary>
        public decimal BaseVAT18 { get; set; }

        /// <summary>VAT at 18%.</summary>
        public decimal AmountVAT18 { get; set; }

        /// <summary>Net amount subject to 8% VAT.</summary>
        public decimal BaseVAT8 { get; set; }

        /// <summary>VAT at 8%.</summary>
        public decimal AmountVAT8 { get; set; }

        /// <summary>Exempt / 0% amount.</summary>
        public decimal BaseVAT0 { get; set; }

        /// <summary>Total without VAT.</summary>
        public decimal TotalWithoutVAT { get; set; }

        /// <summary>Total VAT amount.</summary>
        public decimal TotalVAT { get; set; }

        /// <summary>Total with VAT.</summary>
        public decimal TotalWithVAT { get; set; }

        // ─── Metadata ────────────────────────────────────────────────────────────────

        /// <summary>Payment method (Cash, Card, etc.).</summary>
        [StringLength(50)]
        public string PaymentMethod { get; set; } = "Para në dorë";

        public bool IsFiscal { get; set; } = true;

        [StringLength(100)]
        public string? CashierName { get; set; }

        /// <summary>Month/Year for quick grouping (e.g. "2026-01").</summary>
        [StringLength(10)]
        public string PeriodKey { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
