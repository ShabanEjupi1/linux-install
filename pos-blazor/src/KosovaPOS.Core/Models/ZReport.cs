using System;
using System.ComponentModel.DataAnnotations;

namespace KosovaPOS.Models
{
    /// <summary>
    /// Daily fiscal Z-Report - saved at end of business day.
    /// Used for ATK (Kosovo Tax Administration) declarations.
    /// A Z-Report closes the fiscal day and cannot be re-opened.
    /// </summary>
    public class ZReport
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Business date this report covers (not the time it was generated).</summary>
        public DateTime ReportDate { get; set; }

        /// <summary>Exact moment the report was generated / printed.</summary>
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string GeneratedBy { get; set; } = string.Empty;

        // ─── Sales totals ────────────────────────────────────────────────────────────

        public decimal TotalSalesWithVAT { get; set; }

        public decimal TotalSalesWithoutVAT { get; set; }

        /// <summary>Total VAT collected across all rates.</summary>
        public decimal TotalVAT { get; set; }

        /// <summary>Sales base (net) subject to 18% VAT.</summary>
        public decimal BaseVAT18 { get; set; }

        /// <summary>VAT amount at 18%.</summary>
        public decimal AmountVAT18 { get; set; }

        /// <summary>Sales base (net) subject to 8% VAT (reduced rate).</summary>
        public decimal BaseVAT8 { get; set; }

        /// <summary>VAT amount at 8%.</summary>
        public decimal AmountVAT8 { get; set; }

        /// <summary>Sales base exempt / 0% VAT.</summary>
        public decimal BaseVAT0 { get; set; }

        // ─── Payment breakdown ───────────────────────────────────────────────────────

        public decimal CashAmount { get; set; }

        public decimal CardAmount { get; set; }

        public decimal OtherAmount { get; set; }

        // ─── Transaction counts ──────────────────────────────────────────────────────

        public int TotalTransactions { get; set; }

        public int FiscalTransactions { get; set; }

        public int NonFiscalTransactions { get; set; }

        // ─── Fiscal device info ──────────────────────────────────────────────────────

        [StringLength(50)]
        public string? FiscalDeviceNumber { get; set; }

        /// <summary>Z-Report number from fiscal printer (if available).</summary>
        [StringLength(50)]
        public string? FiscalZNumber { get; set; }

        // ─── ATK submission tracking ─────────────────────────────────────────────────

        public bool IsSubmittedToATK { get; set; } = false;

        public DateTime? SubmittedToATKAt { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
