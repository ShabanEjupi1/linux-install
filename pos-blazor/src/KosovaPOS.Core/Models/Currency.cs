using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models
{
    /// <summary>
    /// Represents a currency supported by the POS for multi-currency payment acceptance.
    /// EUR is always the base currency (IsBase = true).
    /// ExchangeRate is expressed as: 1 EUR = ExchangeRate units of this currency.
    /// e.g. USD ExchangeRate = 1.08 means 1 EUR = 1.08 USD.
    /// </summary>
    [Table("Currencies")]
    public class Currency
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(10)]
        public string Code { get; set; } = "";          // e.g. EUR, USD, CHF

        [Required, StringLength(100)]
        public string Name { get; set; } = "";          // e.g. Euro, Dollar amerikan

        [StringLength(10)]
        public string Symbol { get; set; } = "";        // e.g. €, $, CHF

        [Column(TypeName = "decimal(18,6)")]
        public decimal ExchangeRate { get; set; } = 1m; // 1 EUR = X of this currency

        /// <summary>Only one currency has IsBase = true (EUR).</summary>
        public bool IsBase { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Tracks a queued receipt that was saved locally while SQL Server was offline.
    /// </summary>
    [Table("OfflineQueueLog")]
    public class OfflineQueueLog
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(50)]
        public string TempReceiptId { get; set; } = "";

        public int? SqlReceiptId { get; set; }

        [Required]
        public string ReceiptJson { get; set; } = "";

        public OfflineQueueStatus Status { get; set; } = OfflineQueueStatus.Pending;

        public DateTime QueuedAt { get; set; } = DateTime.Now;
        public DateTime? SyncedAt { get; set; }

        [StringLength(1000)]
        public string? ErrorMessage { get; set; }
    }

    public enum OfflineQueueStatus
    {
        Pending = 0,
        Synced  = 1,
        Failed  = 2
    }

    /// <summary>
    /// Audit log for incoming REST API requests.
    /// </summary>
    [Table("ApiRequestLog")]
    public class ApiRequestLog
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(10)]
        public string Method { get; set; } = "";

        [Required, StringLength(500)]
        public string Path { get; set; } = "";

        public int StatusCode { get; set; }

        [StringLength(100)]
        public string? RemoteIp { get; set; }

        public DateTime RequestedAt { get; set; } = DateTime.Now;

        public int DurationMs { get; set; }
    }
}
