using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Models.BMDData;
using KosovaPOS.Models;
using KosovaPOS.Models.Shop;

namespace KosovaPOS.Core.Services;

/// <summary>
/// One article as the public shop sees it.
///
/// <see cref="Name"/> is the name from the article's <see cref="ShopListing"/>, NOT
/// <c>Artikujt.Emertimi</c> — a customer never sees "Loder 0115012".
/// </summary>
public sealed record ShopProduct(
    long Id,
    string Name,
    string? Description,
    string? Barcode,
    string? Category,
    decimal Price,
    decimal Stock,
    IReadOnlyList<string> Photos)
{
    public bool InStock => Stock > 0;
    public string MainPhoto => Photos.Count > 0 ? Photos[0] : "";
}

/// <summary>What a customer is trying to buy: an article and how many.</summary>
public sealed record CartLine(long ArticleId, decimal Quantity);

public sealed record PlacedOrder(WebOrder Order, string? Error)
{
    public bool Ok => Error is null;
}

/// <summary>
/// The public shop's view of the business: what is for sale, at what price, and how many
/// are left — read live from the same <c>Artikujt</c> table the till sells from. There is
/// no catalogue sync and no product mirror, so a sale at the counter changes what the
/// website says within the cache's lifetime, and a price edited in the POS is the price
/// the customer is charged.
///
/// A product is listed only if it has BOTH a photo and a name — a <see cref="ShopListing"/>
/// with a title. Neither half is a styling preference. This shop's article names are an
/// accountant's names, a generic noun and an internal code ("Loder 0115012" — <i>Toy
/// 0115012</i>), and its articles have no pictures at all in BMD; a listing missing either
/// one tells a customer nothing about what they would be buying. Half a listing does not
/// sell a product, it just makes the shop look broken, so an article stays off the website
/// until a human has done both. <c>/foto</c> is where that happens, and it counts down what
/// is left.
///
/// Out-of-stock products stay listed, greyed out, because the shop wants the catalogue to
/// look like the shop.
/// </summary>
public class ShopService
{
    private readonly IDbContextFactory<PosDbContext> _dbFactory;
    private readonly PosCache _cache;

    public ShopService(IDbContextFactory<PosDbContext> dbFactory, PosCache cache)
    {
        _dbFactory = dbFactory;
        _cache = cache;
    }

    /// <summary>
    /// Everything a customer could actually buy — photographed AND named — in-stock first.
    /// Served from the catalogue cache, which <c>PosDbContext</c> invalidates on every write
    /// that touches <c>Artikujt</c>, a photo, or a listing — so a till sale that empties the
    /// last unit takes the "Add to cart" button off the website, and naming an article puts
    /// it on the website, without anyone rebuilding anything.
    /// </summary>
    public async Task<List<ShopProduct>> GetProductsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await _cache.GetOrLoadAsync(db.DatabaseName, DataVersions.ShopCatalog, async () =>
        {
            // Named first, because it is the scarcer half: a photo without a name is not a
            // listing, so there is no point loading photos for articles nobody has named.
            var listings = await db.ShopListings.AsNoTracking()
                .Where(l => l.Title != null && l.Title != "")
                .ToDictionaryAsync(l => l.ArticleId);

            if (listings.Count == 0)
                return new List<ShopProduct>();

            var named = listings.Keys.ToList();

            var photos = await db.ArticlePhotos.AsNoTracking()
                .Where(p => named.Contains(p.ArticleId))
                .OrderBy(p => p.ArticleId).ThenBy(p => p.SortOrder)
                .Select(p => new { p.ArticleId, p.Url })
                .ToListAsync();

            if (photos.Count == 0)
                return new List<ShopProduct>();

            var byArticle = photos.GroupBy(p => p.ArticleId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(p => p.Url).ToList());

            // Both halves present, so this is exactly the set that can go on the website.
            var ids = byArticle.Keys.ToList();

            var articles = await db.Artikujt.AsNoTracking()
                .Where(a => ids.Contains(a.Id))
                .ToListAsync();

            return articles
                .Select(a => ToProduct(a, listings[a.Id], byArticle[a.Id]))
                // Sellable things first; within each, the newest article the shop added.
                .OrderByDescending(p => p.InStock)
                .ThenByDescending(p => p.Id)
                .ToList();
        });
    }

    public async Task<ShopProduct?> GetProductAsync(long id)
        => (await GetProductsAsync()).FirstOrDefault(p => p.Id == id);

    public async Task<List<string>> GetCategoriesAsync()
        => (await GetProductsAsync())
            .Select(p => p.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

    private static ShopProduct ToProduct(Artikujt a, ShopListing listing, IReadOnlyList<string> photos) => new(
        a.Id,
        // The listing title, never a.Emertimi. GetProductsAsync only reaches here for
        // articles whose listing has one, so there is nothing to fall back to.
        listing.Title!,
        listing.Description,
        a.Barkodi,
        a.Kategoria,
        (decimal)(a.CShitjes ?? 0),
        (decimal)(a.Sasia ?? 0),
        photos);

    // ── Placing an order ────────────────────────────────────────────────

    /// <summary>
    /// Turns a cart into a <see cref="WebOrder"/>, priced from the database rather than
    /// from anything the browser sent.
    ///
    /// The cart cookie carries article ids and quantities and NOTHING ELSE — no prices.
    /// Every unit price and every line total here is read from <c>Artikujt</c> at this
    /// moment. A cart cookie is client-side state, which means it is attacker-authored
    /// state, and a shop that trusts a price out of one sells €200 coats for €0.01.
    ///
    /// Stock is NOT decremented here. The order exists, unpaid; stock moves in
    /// <see cref="ConfirmAsync"/> once money has actually arrived (or immediately, for
    /// cash-on-delivery). Reserving stock at this point would let anyone empty the shop's
    /// inventory by filling carts they never pay for.
    /// </summary>
    public async Task<PlacedOrder> PlaceOrderAsync(
        IReadOnlyList<CartLine> cart,
        WebOrder details,
        BusinessSettings settings)
    {
        if (cart.Count == 0)
            return new PlacedOrder(details, "Shporta është bosh.");

        await using var db = await _dbFactory.CreateDbContextAsync();

        var ids = cart.Select(c => c.ArticleId).Distinct().ToList();
        var articles = await db.Artikujt.AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        // What the shop actually offers online, re-checked here and not taken on trust from
        // the cart. Existing in Artikujt is not the same as being for sale: 1619 articles are
        // in that table and only the named, photographed ones are on the website. Since the
        // cart cookie is attacker-authored (above), a cart that skipped this check would let
        // anyone order any article in the shop's accounting system by editing a cookie.
        var listings = await db.ShopListings.AsNoTracking()
            .Where(l => ids.Contains(l.ArticleId) && l.Title != null && l.Title != "")
            .ToDictionaryAsync(l => l.ArticleId);

        var items = new List<WebOrderItem>();
        foreach (var line in cart)
        {
            if (line.Quantity <= 0)
                continue;

            if (!articles.TryGetValue(line.ArticleId, out var a) ||
                !listings.TryGetValue(line.ArticleId, out var listing))
                return new PlacedOrder(details, "Një artikull në shportë nuk shitet më.");

            // The name the customer chose it by. a.Emertimi ("Loder 0115012") is the
            // accountant's name and must never reach a customer, an order or an email.
            var name = listing.Title!;

            var stock = (decimal)(a.Sasia ?? 0);
            if (stock < line.Quantity)
                return new PlacedOrder(details,
                    $"\"{name}\" — vetëm {stock:0.##} copë në gjendje.");

            var price = (decimal)(a.CShitjes ?? 0);
            if (price <= 0)
                return new PlacedOrder(details, $"\"{name}\" nuk ka çmim shitjeje.");

            items.Add(new WebOrderItem
            {
                ArticleId = a.Id,
                Name = name,
                Barcode = a.Barkodi,
                UnitPrice = price,
                Quantity = line.Quantity,
                LineTotal = decimal.Round(price * line.Quantity, 2)
            });
        }

        if (items.Count == 0)
            return new PlacedOrder(details, "Shporta është bosh.");

        var subtotal = items.Sum(i => i.LineTotal);
        var shipping = ShippingFor(subtotal, settings);

        details.Items = items;
        details.Subtotal = subtotal;
        details.ShippingFee = shipping;
        details.Total = subtotal + shipping;
        details.OrderNumber = NewOrderNumber();
        details.CreatedAt = DateTime.Now;
        details.UpdatedAt = DateTime.Now;

        db.WebOrders.Add(details);
        await db.SaveChangesAsync();

        return new PlacedOrder(details, null);
    }

    public static decimal ShippingFor(decimal subtotal, BusinessSettings s)
        => s.ShopFreeShippingOver > 0 && subtotal >= s.ShopFreeShippingOver
            ? 0m
            : s.ShopShippingFee;

    /// <summary>
    /// Time-ordered and short enough to read down a phone. Not sequential: a customer
    /// must not be able to count the shop's orders by subtracting two of them.
    /// </summary>
    private static string NewOrderNumber()
        => $"E{DateTime.Now:yyMMdd}-{Random.Shared.Next(1000, 9999)}";

    public async Task<WebOrder?> FindByPayPalOrderAsync(string payPalOrderId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.WebOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.PayPalOrderId == payPalOrderId);
    }

    public async Task<WebOrder?> FindByNumberAsync(string orderNumber)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.WebOrders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
    }

    public async Task SetPayPalOrderIdAsync(int orderId, string payPalOrderId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var order = await db.WebOrders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null) return;
        order.PayPalOrderId = payPalOrderId;
        order.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// The order is real: take its lines out of stock, once, and record where the payment
    /// stands.
    ///
    /// Called on two different occasions, which is why payment status is a parameter rather
    /// than assumed. A PayPal order is confirmed once the money is captured
    /// (<see cref="WebPaymentStatus.Paid"/>). A cash-on-delivery order is confirmed the
    /// moment it is placed, still <see cref="WebPaymentStatus.Pending"/> — nothing has been
    /// paid, but the shop has committed to shipping it, so the goods must leave stock or the
    /// till will sell them to someone else this afternoon.
    ///
    /// Stock moves through <see cref="Data.StockMovementSql.MoveStockAsync"/>, like every
    /// other stock write in the app: the arithmetic happens in the database
    /// (<c>Sasia = Sasia - n</c>) so a till sale landing between our read and our write is
    /// not silently erased, and it stamps the catalogue cache itself since it bypasses the
    /// change tracker. Both of those were bugs once — a web order that hand-rolled its own
    /// subtraction here would reintroduce them, one at a time.
    ///
    /// <see cref="WebOrder.StockTaken"/> makes it idempotent. PayPal calls a return URL
    /// more than once — the customer refreshes it, the browser retries it — and stock must
    /// come off exactly once regardless.
    /// </summary>
    public async Task<bool> ConfirmAsync(int orderId, WebPaymentStatus payment, string? captureId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var order = await db.WebOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null) return false;

        if (order.StockTaken)
        {
            // Already confirmed — this is the replayed callback. The payment status may still
            // need to move (a cash-on-delivery order that gets paid), but stock does not.
            if (order.PaymentStatus != payment)
            {
                order.PaymentStatus = payment;
                order.PayPalCaptureId ??= captureId;
                order.UpdatedAt = DateTime.Now;
                await db.SaveChangesAsync();
            }
            await tx.CommitAsync();
            return true;
        }

        foreach (var item in order.Items)
        {
            var sold = (double)item.Quantity;
            await db.MoveStockAsync(item.ArticleId, delta: -sold, outQty: sold);
        }

        order.PaymentStatus = payment;
        order.PayPalCaptureId = captureId;
        order.StockTaken = true;
        order.UpdatedAt = DateTime.Now;

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return true;
    }

    public async Task MarkFailedAsync(int orderId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var order = await db.WebOrders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null) return;
        order.PaymentStatus = WebPaymentStatus.Failed;
        order.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
    }

    // ── The shop's own order list ───────────────────────────────────────

    public async Task<List<WebOrder>> ListOrdersAsync(int take = 200)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.WebOrders.AsNoTracking()
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task SetStatusAsync(int orderId, WebOrderStatus status)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var order = await db.WebOrders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null) return;
        order.Status = status;
        order.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
    }
}
