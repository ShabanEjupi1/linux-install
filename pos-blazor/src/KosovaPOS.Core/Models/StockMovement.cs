using System.ComponentModel.DataAnnotations;

namespace KosovaPOS.Models;

public enum StockMovementType
{
    /// <summary>Goods received / manual increase.</summary>
    In = 1,
    /// <summary>Goods issued, written off, or otherwise removed.</summary>
    Out = 2,
    /// <summary>Physical count correction: stock is set to the counted figure.</summary>
    Count = 3
}

/// <summary>
/// Append-only journal of every manual stock change made from the web app.
/// The desktop kept this in SQL Server `InventoryMovements`, which was never
/// part of the BMD export and so has no rows in Postgres; this is the web-side
/// replacement. Rows are never updated or deleted — a mistaken movement is
/// corrected by recording the opposite one.
/// </summary>
public class StockMovement
{
    [Key]
    public int Id { get; set; }

    /// <summary>Artikujt.Id — the BMD article this movement applied to.</summary>
    public long ArticleId { get; set; }

    [StringLength(50)]
    public string Barcode { get; set; } = string.Empty;

    [StringLength(250)]
    public string ArticleName { get; set; } = string.Empty;

    [StringLength(20)]
    public string Unit { get; set; } = "Copë";

    public StockMovementType Type { get; set; }

    /// <summary>Signed change applied to stock: positive for In, negative for Out.</summary>
    public decimal Quantity { get; set; }

    public decimal QuantityBefore { get; set; }
    public decimal QuantityAfter { get; set; }

    /// <summary>Purchase price at the time of the movement, for valuing it later.</summary>
    public decimal UnitCost { get; set; }

    public DateTime MovedAt { get; set; } = DateTime.Now;

    [StringLength(100)]
    public string? UserName { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    /// <summary>Human-readable handle, e.g. WEB-IN-20260710143000.</summary>
    [StringLength(60)]
    public string Reference { get; set; } = string.Empty;
}
