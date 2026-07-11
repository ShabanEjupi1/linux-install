using KosovaPOS.Agent.Contracts;
using KosovaPOS.Models;

namespace KosovaPOS.Core.Services.Hardware;

/// <summary>
/// Maps domain objects to the agent wire contracts. Keeps the Blazor pages free
/// of formatting/transport concerns — a page just hands a <see cref="Receipt"/>
/// (plus the shop's business settings) to the bridge.
/// </summary>
public static class HardwareMapper
{
    /// <summary>Builds the fiscal print request (F-Link INP payload) from a receipt.</summary>
    public static FiscalPrintRequest ToFiscalRequest(Receipt receipt, int timeoutSeconds = 30) => new()
    {
        Payload = FiscalReceiptBuilder.Build(receipt),
        ReceiptNumber = receipt.ReceiptNumber,
        TimeoutSeconds = timeoutSeconds,
    };

    /// <summary>Builds the non-fiscal receipt print request the agent renders locally.</summary>
    public static ReceiptPrintRequest ToReceiptRequest(Receipt receipt, BusinessSettings? shop = null) => new()
    {
        BusinessName = shop?.BusinessName ?? "",
        Address = shop?.Address,
        FiscalNumber = shop?.FiscalNumber,
        ReceiptNumber = receipt.ReceiptNumber,
        Date = new DateTimeOffset(receipt.Date),
        CashierName = receipt.CashierName,
        PaymentMethod = receipt.PaymentMethod,
        Lines = receipt.Items.Select(i => new ReceiptLineDto
        {
            Name = i.ArticleName,
            Quantity = i.Quantity,
            UnitPrice = i.Price,
            LineTotal = i.TotalValue,
            VatRate = i.VATRate,
        }).ToList(),
        Subtotal = receipt.Items.Sum(i => i.TotalValue - i.VATValue),
        Vat = receipt.Items.Sum(i => i.VATValue),
        Total = receipt.TotalAmount,
        Paid = receipt.PaidAmount,
        Change = receipt.PaidAmount - receipt.TotalAmount < 0 ? 0 : receipt.PaidAmount - receipt.TotalAmount,
        // Let the shop point its courtesy receipt at any thermal printer; null falls
        // back to the agent's RECEIPT_PRINTER default on the cashier PC.
        PrinterName = string.IsNullOrWhiteSpace(shop?.ReceiptPrinter) ? null : shop!.ReceiptPrinter,
    };

    public static BarcodePrintRequest ToBarcodeRequest(Article article, int copies = 1, string? printerName = null) => new()
    {
        Barcode = article.Barcode,
        ArticleName = article.Name,
        Price = article.SalesPrice,
        Copies = copies,
        PrinterName = printerName,
    };
}
