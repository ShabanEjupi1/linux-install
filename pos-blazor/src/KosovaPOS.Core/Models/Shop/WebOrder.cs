using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.Shop;

public enum WebOrderStatus
{
    /// <summary>Placed, not yet looked at by the shop.</summary>
    New = 0,
    Confirmed = 1,
    Shipped = 2,
    Delivered = 3,
    Cancelled = 4
}

public enum WebPaymentMethod
{
    PayPal = 0,
    CashOnDelivery = 1
}

public enum WebPaymentStatus
{
    /// <summary>PayPal order created, customer has not approved it yet. Nothing is owed.</summary>
    Pending = 0,
    Paid = 1,
    Failed = 2,
    Refunded = 3
}

/// <summary>
/// An order placed on the public shop.
///
/// Deliberately NOT a <c>Receipt</c>. A receipt in this system is a fiscal document: it
/// takes a number from the shared journal, it is what the tax authority is told about,
/// and Phase 26 had to put an advisory lock around minting one. A web order is a
/// *request to buy* — it can be cancelled, it can fail payment, it can sit unpaid for a
/// day. Fusing the two would mean either issuing fiscal receipts for orders that never
/// get paid, or holding a journal lock across a PayPal round-trip. The shop turns an
/// order into a receipt when it actually hands the goods over, and <see cref="ReceiptId"/>
/// is where that link is recorded.
/// </summary>
[Table("WebOrders")]
public class WebOrder
{
    public int Id { get; set; }

    /// <summary>What the customer is told to quote. Human-shaped, not the primary key.</summary>
    [Required, StringLength(20)]
    public string OrderNumber { get; set; } = "";

    [Required, StringLength(200)]
    public string CustomerName { get; set; } = "";

    [Required, StringLength(200)]
    public string Email { get; set; } = "";

    [StringLength(50)]
    public string? Phone { get; set; }

    [Required, StringLength(500)]
    public string Address { get; set; } = "";

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ShippingFee { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }

    public WebPaymentMethod PaymentMethod { get; set; }
    public WebPaymentStatus PaymentStatus { get; set; } = WebPaymentStatus.Pending;
    public WebOrderStatus Status { get; set; } = WebOrderStatus.New;

    /// <summary>PayPal's id for the order we created. Our handle on it until it is captured.</summary>
    [StringLength(100)]
    public string? PayPalOrderId { get; set; }

    /// <summary>PayPal's id for the money actually taken. Present only once paid.</summary>
    [StringLength(100)]
    public string? PayPalCaptureId { get; set; }

    /// <summary>Set when the shop turns this order into a till sale.</summary>
    public int? ReceiptId { get; set; }

    /// <summary>
    /// True once this order's lines have been taken out of <c>Artikujt.Sasia</c>. Stock is
    /// decremented exactly once, at payment, and this flag is what makes that idempotent:
    /// PayPal will happily call a return URL twice, and the customer can refresh it.
    /// </summary>
    public bool StockTaken { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public List<WebOrderItem> Items { get; set; } = new();
}

[Table("WebOrderItems")]
public class WebOrderItem
{
    public int Id { get; set; }

    public int WebOrderId { get; set; }
    public WebOrder? Order { get; set; }

    /// <summary>FK to <c>Artikujt.id</c>.</summary>
    public long ArticleId { get; set; }

    /// <summary>
    /// Copied at order time, not joined at read time. The shop edits prices and renames
    /// articles; an order must still say what the customer was actually charged and what
    /// they thought they were buying, a year later, after the article has been repriced
    /// or deleted outright.
    /// </summary>
    [Required, StringLength(250)]
    public string Name { get; set; } = "";

    [StringLength(50)]
    public string? Barcode { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }
}
