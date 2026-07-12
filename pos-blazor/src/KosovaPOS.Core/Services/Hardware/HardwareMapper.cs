using KosovaPOS.Agent.Contracts;
using KosovaPOS.Core.Printing;
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

    /// <summary>
    /// Wraps an already-laid-out receipt for the agent. The layout arrives from
    /// <see cref="ReceiptFormatter"/> — the same lines the browser renders at
    /// <c>/kupon/{n}</c> — so the paper and the screen cannot show different documents.
    /// </summary>
    public static ReceiptPrintRequest ToReceiptRequest(
        string receiptNumber, IEnumerable<ReceiptTextLine> lines, BusinessSettings? shop = null) => new()
    {
        ReceiptNumber = receiptNumber,
        Lines = lines.Select(l => new ReceiptDocumentLine
        {
            Text = l.Text,
            Emphasis = (int)l.Emphasis,
            // Carried through, or the encoding sample prints all seven of its lines under one
            // page and every one of them looks identical — which is the opposite of its job.
            CodePage = l.CodePage,
        }).ToList(),
        // Let the shop point its courtesy receipt at any thermal printer; null falls
        // back to the agent's RECEIPT_PRINTER default on the cashier PC.
        PrinterName = string.IsNullOrWhiteSpace(shop?.ReceiptPrinter) ? null : shop!.ReceiptPrinter,
        CodePage = string.IsNullOrWhiteSpace(shop?.ReceiptCodePage) ? null : shop!.ReceiptCodePage,
    };

    public static BarcodePrintRequest ToBarcodeRequest(
        Article article, int copies = 1, string? printerName = null,
        int labelWidthMm = 55, int labelHeightMm = 25) => new()
    {
        Barcode = article.Barcode,
        ArticleName = article.Name,
        Price = article.SalesPrice,
        Copies = copies,
        PrinterName = printerName,
        LabelWidthMm = labelWidthMm,
        LabelHeightMm = labelHeightMm,
    };
}
