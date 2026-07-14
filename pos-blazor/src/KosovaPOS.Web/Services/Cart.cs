using KosovaPOS.Core.Services;

namespace KosovaPOS.Web.Services;

/// <summary>
/// The customer's basket, kept in a cookie.
///
/// A cookie rather than a database row because the shop has no customer accounts and never
/// will: a basket that outlives the browser session would need an identity to belong to.
/// It survives navigation and a closed tab, which is all a basket has to do.
///
/// The format — and the rule that it carries no prices — lives in <see cref="CartCookie"/>.
/// This class is only the cookie plumbing around it.
/// </summary>
public sealed class Cart
{
    private readonly IHttpContextAccessor _http;

    public Cart(IHttpContextAccessor http) => _http = http;

    public IReadOnlyList<CartLine> Lines => Read();

    public decimal Count => Read().Sum(l => l.Quantity);

    public bool IsEmpty => Read().Count == 0;

    private List<CartLine> Read()
        => CartCookie.Parse(_http.HttpContext?.Request.Cookies[CartCookie.Name]);

    private void Write(List<CartLine> lines)
    {
        var context = _http.HttpContext;
        if (context is null) return;

        if (lines.Count == 0)
        {
            context.Response.Cookies.Delete(CartCookie.Name);
            return;
        }

        context.Response.Cookies.Append(CartCookie.Name, CartCookie.Serialise(lines), new CookieOptions
        {
            HttpOnly = true,       // nothing in the browser needs to read it
            Secure = true,
            SameSite = SameSiteMode.Lax,   // must survive the return trip from PayPal
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            IsEssential = true,    // a shop without a basket is not a shop
            Path = "/",
        });
    }

    public void Add(long articleId, decimal quantity = 1)
    {
        var lines = Read();
        var existing = lines.FindIndex(l => l.ArticleId == articleId);

        if (existing >= 0)
            lines[existing] = lines[existing] with { Quantity = lines[existing].Quantity + quantity };
        else
            lines.Add(new CartLine(articleId, quantity));

        Write(lines);
    }

    public void SetQuantity(long articleId, decimal quantity)
    {
        var lines = Read();
        lines.RemoveAll(l => l.ArticleId == articleId);

        if (quantity > 0)
            lines.Add(new CartLine(articleId, quantity));

        Write(lines);
    }

    public void Remove(long articleId)
    {
        var lines = Read();
        lines.RemoveAll(l => l.ArticleId == articleId);
        Write(lines);
    }

    public void Clear() => Write([]);
}
