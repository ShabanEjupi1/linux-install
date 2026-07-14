using KosovaPOS.Core.Services;
using KosovaPOS.Models;
using Xunit;

namespace KosovaPOS.Tests;

/// <summary>
/// The rules that decide what a customer is charged, and which products may be looked up
/// automatically. No database — these are pure functions, and they are the ones that would
/// quietly cost money if they were wrong.
/// </summary>
public class ShopPricingTests
{
    // ── Shipping ────────────────────────────────────────────────────────

    [Fact]
    public void Shipping_is_charged_below_the_free_threshold()
    {
        var s = new BusinessSettings { ShopShippingFee = 2.00m, ShopFreeShippingOver = 30m };
        Assert.Equal(2.00m, ShopService.ShippingFor(29.99m, s));
    }

    [Fact]
    public void Shipping_is_free_at_and_above_the_threshold()
    {
        var s = new BusinessSettings { ShopShippingFee = 2.00m, ShopFreeShippingOver = 30m };

        // Exactly on the line is free: a customer who spends precisely the advertised amount
        // and is still charged for delivery has been lied to by the banner.
        Assert.Equal(0m, ShopService.ShippingFor(30m, s));
        Assert.Equal(0m, ShopService.ShippingFor(100m, s));
    }

    [Fact]
    public void A_zero_threshold_means_shipping_is_always_charged()
    {
        var s = new BusinessSettings { ShopShippingFee = 2.50m, ShopFreeShippingOver = 0m };

        // The trap this guards: "free over 0 €" read literally would make delivery free on
        // every order in the shop, and the setting's default is 0.
        Assert.Equal(2.50m, ShopService.ShippingFor(0.01m, s));
        Assert.Equal(2.50m, ShopService.ShippingFor(10_000m, s));
    }

    // ── The cart cookie ─────────────────────────────────────────────────
    //
    // A cookie is a value the customer can rewrite. These tests pin down that the cart
    // carries no prices at all — only ids and quantities — and that garbage in it produces
    // an empty basket rather than an exception on the shop's front page.

    [Fact]
    public void A_cart_cookie_carries_only_ids_and_quantities()
    {
        var lines = CartCookie.Parse("12:2,88:1.5");

        Assert.Equal(2, lines.Count);
        Assert.Equal(12, lines[0].ArticleId);
        Assert.Equal(2m, lines[0].Quantity);
        Assert.Equal(88, lines[1].ArticleId);
        Assert.Equal(1.5m, lines[1].Quantity);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("garbage")]
    [InlineData("12")]              // no quantity
    [InlineData("12:abc")]          // quantity is not a number
    [InlineData("abc:2")]           // id is not a number
    [InlineData("12:0")]            // zero quantity is not a line
    [InlineData("12:-4")]           // nor is a negative one
    [InlineData("-12:4")]           // nor is a negative id
    [InlineData("12:2:3")]          // malformed
    public void A_malformed_cart_cookie_yields_no_lines(string? raw)
    {
        // Never an exception. A hand-edited cookie must produce an empty basket, not a 500
        // on the storefront — and a negative quantity must never become a negative charge.
        Assert.Empty(CartCookie.Parse(raw));
    }

    [Fact]
    public void A_cart_quantity_is_capped()
    {
        // The quantity is attacker-supplied and gets multiplied by a price. Stock is checked
        // at checkout regardless, so this only keeps the arithmetic sane on the way there.
        var lines = CartCookie.Parse("12:999999");
        Assert.Equal(999m, lines[0].Quantity);
    }

    // ── Barcode validity ────────────────────────────────────────────────
    //
    // This gate is why automatic photo lookup is safe at all: only a real EAN names a real
    // product. Most of this shop's "barcodes" are internal codes, and sending those to a
    // product database returns a confidently wrong item.

    [Theory]
    [InlineData("8997011600805")]   // real EAN-13s from the shop's own catalogue
    [InlineData("8680941630102")]
    [InlineData("8692660112074")]
    [InlineData("3905354310072")]
    public void Real_EAN13s_are_accepted(string barcode)
        => Assert.True(BarcodeImageLookup.IsValidEan(barcode));

    [Theory]
    [InlineData("00080")]           // the shop's internal codes, which look nothing like EANs
    [InlineData("29")]
    [InlineData("0327")]
    [InlineData("365739929")]       // 9 digits: not a length any EAN has
    [InlineData("8997011600806")]   // a real EAN with the check digit changed
    [InlineData("899701160080X")]   // not all digits
    [InlineData("")]
    [InlineData(null)]
    public void Everything_else_is_rejected(string? barcode)
        => Assert.False(BarcodeImageLookup.IsValidEan(barcode));

    // ── Shop domains ────────────────────────────────────────────────────

    [Theory]
    [InlineData("enisi.tech", "enisi.tech")]
    [InlineData("  Enisi.Tech  ", "enisi.tech")]
    [InlineData("https://enisi.tech", "enisi.tech")]
    [InlineData("http://www.enisi.tech/", "enisi.tech")]
    [InlineData("https://enisi.tech/dyqani", "enisi.tech")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void A_shop_domain_is_normalised_to_a_bare_host(string? input, string? expected)
    {
        // Whatever the operator pastes into the console has to end up equal to what a browser
        // puts in the Host header, or the domain silently resolves to no business at all.
        Assert.Equal(expected, BusinessRegistry.NormaliseShopDomain(input));
    }
}
