using KosovaPOS.Core.Services.Hardware;
using KosovaPOS.Models;
using Xunit;

namespace KosovaPOS.Tests;

/// <summary>
/// The INP payload F-Link consumes. The one that bit us: F-Link only ever parsed CRLF line
/// endings (the desktop app wrote them via <c>AppendLine</c> on Windows), but this builder now
/// runs server-side on Linux, where a naive <c>\n</c> produced a Fatura.inp that landed in the
/// watched folder and then silently never printed. These lock the ending in.
/// </summary>
public class FiscalReceiptBuilderTests
{
    private static Receipt SampleReceipt() => new()
    {
        ReceiptNumber = "2026-0001",
        TotalAmount = 1.50m,
        PaidAmount = 2.00m,
        Items =
        {
            new ReceiptItem { ArticleName = "Buke", Price = 0.50m, Quantity = 1m, VATRate = 18m },
            new ReceiptItem { ArticleName = "Uje", Price = 0.50m, Quantity = 2m, VATRate = 8m },
        },
    };

    [Fact]
    public void Every_line_ends_in_crlf_not_bare_lf()
    {
        var payload = FiscalReceiptBuilder.Build(SampleReceipt());

        // No LF may appear that is not preceded by CR — that is exactly the file F-Link ignores.
        for (var i = 0; i < payload.Length; i++)
        {
            if (payload[i] == '\n')
                Assert.True(i > 0 && payload[i - 1] == '\r',
                    $"Found a bare LF at index {i}; F-Link requires CRLF.");
        }
    }

    [Fact]
    public void Builds_the_expected_flink_lines()
    {
        var payload = FiscalReceiptBuilder.Build(SampleReceipt());
        var lines = payload.Split("\r\n", System.StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("S,1,______,_,__;Buke;0.50;1.00;1;1;3;0;0;0;0", lines[0]);
        Assert.Equal("S,1,______,_,__;Uje;0.50;2.00;2;1;3;0;0;0;0", lines[1]);
        Assert.Equal("Q,1,______,_,__;1;Pagoi: 2.00", lines[2]);
        Assert.Equal("Q,1,______,_,__;2;Kusur: 0.50", lines[3]);
        Assert.Equal("T,1,______,_,__;", lines[4]);
        // And the file itself ends in CRLF (the terminating T line is CRLF-closed).
        Assert.EndsWith("T,1,______,_,__;\r\n", payload);
    }
}
