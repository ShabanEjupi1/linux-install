using KosovaPOS.Web;
using KosovaPOS.Web.Components;
using KosovaPOS.Web.Services;
using KosovaPOS.Core.Data;
using KosovaPOS.Core.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

// Before any DbContext is touched: this configures how Npgsql maps DateTime for
// the whole process, and it is read when a data source is built, not when a query
// runs. Setting it here rather than leaning on a static constructor keeps it
// independent of which context happens to be opened first.
KosovaPOS.Core.Data.NpgsqlCompat.EnableLegacyTimestampBehavior();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── EF Core / Postgres ──────────────────────────────────────────────────
// One Postgres server, one credential, one database per business. The resolved
// string is a *template*: PosDbContextFactory swaps its Database= for whichever
// business the signed-in user belongs to. The database it names is the primary
// business — the shop that existed before multi-business, whose data never moved.
//
// Resolves from (in order): env POS_DB_CONNECTION, config "ConnectionStrings:Postgres",
// then a local-dev default.
var connectionTemplate =
    Environment.GetEnvironmentVariable("POS_DB_CONNECTION")
    ?? builder.Configuration.GetConnectionString("Postgres")
    ?? KosovaPOS.Web.Data.DesignTimeConnection.LocalDevDefault;

builder.Services.AddSingleton(new PosDbContextFactory(connectionTemplate));

// The control database: the business registry and the platform admins. Fixed
// name, so it is derived from the template rather than configured separately.
builder.Services.AddDbContextFactory<ControlDbContext>((sp, options) =>
    options.UseNpgsql(sp.GetRequiredService<PosDbContextFactory>().ControlConnectionString));

builder.Services.AddSingleton<BusinessRegistry>();
builder.Services.AddSingleton<BusinessProvisioner>();

// Each business is reachable at pos-<code>.<POS_BASE_HOST>. The prefix keeps
// tenant hostnames out of the infra namespace and, being first-level, inside the
// free *.<base> wildcard cert. POS_HOST_PREFIX may be set empty to drop it.
var baseHost = Environment.GetEnvironmentVariable("POS_BASE_HOST") ?? "spacecode.tech";
var hostPrefix = Environment.GetEnvironmentVariable("POS_HOST_PREFIX") ?? "pos-";
builder.Services.AddSingleton(new BusinessHostResolver(baseHost, hostPrefix));

// IDbContextFactory<PosDbContext> is NOT registered via AddDbContextFactory: that
// would bind one connection string for the whole process. Instead a scoped factory
// picks the database from the signed-in user's bizid claim, on every context it
// opens. In Blazor Server a scope is the circuit, which belongs to one user.
builder.Services.AddScoped<CurrentBusiness>();
builder.Services.AddScoped<IDbContextFactory<PosDbContext>, TenantDbContextFactory>();

// ── Domain services (from Core) ─────────────────────────────────────────

// Singleton, and it must be: it is what stops six screens re-reading the same 1,600-row
// catalogue from Postgres every time somebody walks between them. Per-circuit it would cache
// nothing that outlived one user's page. Invalidated by PosDbContext on save, never by a timer.
builder.Services.AddSingleton<PosCache>();

// ── Online shop ─────────────────────────────────────────────────────────
// Product photos are files. In production POS_MEDIA_DIR is a mounted volume, so they
// survive a redeploy; in dev it falls back under the content root.
var mediaDir = Environment.GetEnvironmentVariable("POS_MEDIA_DIR")
               ?? Path.Combine(builder.Environment.ContentRootPath, "media");
builder.Services.AddSingleton(new MediaStore(mediaDir));

// Credentials come from the environment, never from the database or the repo.
builder.Services.AddSingleton(EmailOptions.FromEnvironment());
builder.Services.AddSingleton(PayPalOptions.FromEnvironment());

// Timeouts, because all three of these call hosts we do not control: a supplier's image
// CDN, PayPal, and a public barcode database. The default HttpClient waits 100 seconds,
// which is 100 seconds of a checkout page doing nothing.
builder.Services.AddHttpClient(nameof(PhotoService), c => c.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddHttpClient(nameof(PayPalService), c => c.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddHttpClient(nameof(BarcodeImageLookup), c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
    // Open Food Facts asks callers to identify themselves, and rejects the default agent.
    c.DefaultRequestHeaders.UserAgent.ParseAdd("KosovaPOS-Shop/1.0 (+https://spacecode.tech)");
});

builder.Services.AddScoped<ShopService>();
builder.Services.AddScoped<PhotoService>();
builder.Services.AddScoped<BarcodeImageLookup>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddSingleton<PayPalService>();
builder.Services.AddScoped<KosovaPOS.Web.Services.Cart>();

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PlatformAuthService>();
builder.Services.AddScoped<BusinessProfileService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<SalesService>();
builder.Services.AddScoped<PurchaseService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<VatBookService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<PartnerService>();
builder.Services.AddScoped<ShiftService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<ReturnService>();
builder.Services.AddScoped<FinanceService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<KosovaPOS.Web.Services.Audit>();
builder.Services.AddScoped<KosovaPOS.Web.Services.HardwareBridge>();
builder.Services.AddScoped<KosovaPOS.Web.Services.LocalState>();

// Singleton: the package on disk does not change while the process runs.
builder.Services.AddSingleton<KosovaPOS.Web.Services.AgentPackage>();
builder.Services.AddSingleton<KosovaPOS.Web.Services.AgentPackageBuilder>();

// Singleton: the failed-login counters are process-wide state, and a lockout that
// reset with every circuit would lock nobody out.
builder.Services.AddSingleton<LoginThrottle>();

// ── Authentication (cookie) ─────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // Permission claims are baked into the cookie at sign-in, so adding a
        // permission makes every outstanding cookie stale (it would be denied
        // pages it should reach). Bumping the name forces one clean re-login.
        //
        // v3: sessions now carry the business they are pinned to. A v2 cookie
        // names no database, so it must not be honoured.
        options.Cookie.Name = "KosovaPOS.Auth.v3";

        // Share the cookie across every business subdomain so an apex login can
        // redirect the user straight to pos-<code>.<zone> already signed in, and a
        // platform admin can impersonate into a shop on its own host. Set to
        // ".spacecode.tech" in production (POS_COOKIE_DOMAIN); left unset in dev so
        // the cookie stays host-only for localhost. Crossing to another shop's host
        // is still refused by the bizid-vs-host middleware below, so a wide cookie
        // widens convenience, not reach.
        var cookieDomain = Environment.GetEnvironmentVariable("POS_COOKIE_DOMAIN");
        if (!string.IsNullOrWhiteSpace(cookieDomain))
            options.Cookie.Domain = cookieDomain;

        options.LoginPath = "/login";
        options.AccessDeniedPath = "/nuk-keni-leje";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization(options =>
{
    // A page must enforce the permission its nav link is gated on — hiding the
    // link alone leaves the route reachable by typing the URL.
    foreach (var perm in AuthService.AllPermissions)
        options.AddPolicy($"perm:{perm}", p => p.RequireClaim(AuthService.PermissionClaim, perm));

    // Any page that touches business data. Keeps a platform admin — who has no
    // business, and therefore no database — out of pages whose services would
    // throw the moment they opened a DbContext.
    options.AddPolicy(AuthService.BusinessPolicy,
        p => p.RequireClaim(AuthService.BusinessIdClaim));

    options.AddPolicy(AuthService.PlatformPolicy,
        p => p.RequireClaim(AuthService.PlatformAdminClaim, "true"));
});
builder.Services.AddCascadingAuthenticationState();

// Persist DataProtection keys (auth cookie protection) to a mounted volume in
// production so sessions survive container restarts/redeploys.
var keysDir = Environment.GetEnvironmentVariable("POS_KEYS_DIR");
if (!string.IsNullOrWhiteSpace(keysDir))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysDir))
        .SetApplicationName("KosovaPOS");
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthenticationStateProvider, PosAuthStateProvider>();

var app = builder.Build();

// ── Provision the control database and every business ───────────────────
// Gate with POS_SKIP_DB_INIT=true to opt out.
if (!string.Equals(Environment.GetEnvironmentVariable("POS_SKIP_DB_INIT"), "true",
        StringComparison.OrdinalIgnoreCase))
{
    await StartupProvisioning.RunAsync(app.Services);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Product photos. Served from a mounted volume rather than wwwroot, because they are
// data the shop creates, not an asset the build ships — a redeploy replaces wwwroot.
{
    var media = app.Services.GetRequiredService<MediaStore>();
    Directory.CreateDirectory(media.Root);

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(media.Root),
        RequestPath = MediaStore.UrlPrefix,
        OnPrepareResponse = ctx =>
        {
            // The filename is a hash of the file's own bytes, so a given URL can never point
            // at different bytes later. That makes it safe to cache hard and forever, which
            // is what keeps a product grid of 40 images off the server on every visit.
            ctx.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        }
    });
}

// ── The public shop's domain ────────────────────────────────────────────
// enisi.tech and pos-811274183.spacecode.tech are the same application and the same
// database. What separates them is this: on a shop domain, the ONLY thing that answers
// is the shop.
//
// Without it, the shop's own domain would also serve /login, /sale, /perdoruesit —
// the entire till, on a hostname handed out to customers. Even though every one of
// those pages is behind an authorization policy and would refuse to render, publishing
// a login form on the shop's front door is an invitation to credential-stuff it, and
// the first thing an attacker does with a new domain is walk its routes.
//
// So: "/" becomes the storefront, the storefront's own paths pass, static assets pass,
// and everything else on that hostname is 404 — as far as the internet can tell, no POS
// lives here.
//
// It must run BEFORE routing, and routing must therefore be called explicitly below —
// otherwise ASP.NET inserts UseRouting at the very top of the pipeline, the endpoint for
// "/" is chosen before this code runs, and rewriting the path here changes nothing except
// the URL that the already-selected (authorized) Home page redirects to. That failure is
// silent and looks exactly like a broken login loop.
app.Use(async (ctx, next) =>
{
    var registry = ctx.RequestServices.GetRequiredService<BusinessRegistry>();
    var shop = await registry.GetActiveByShopDomainAsync(ctx.Request.Host.Value);

    if (shop is null)
    {
        await next();   // a POS host, or the apex: nothing changes
        return;
    }

    var path = ctx.Request.Path.Value ?? "/";

    if (path == "/" || path.Length == 0)
    {
        // Rewritten, not redirected: the shop's front page is enisi.tech/, not
        // enisi.tech/dyqani. Customers link to the former and so do search engines.
        ctx.Request.Path = "/dyqani";
        await next();
        return;
    }

    var allowed =
        path.StartsWith("/dyqani", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(MediaStore.UrlPrefix, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/_content", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase) ||
        // Static assets under wwwroot — app.css, favicon, the Bootstrap bundle. They all
        // carry an extension; no page route in this application does.
        Path.HasExtension(path);

    if (!allowed)
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});

// A business host that names no active business is not this application. One
// *.<base> DNS record and one ingress rule route EVERY pos-<label> host here, so
// without this the app answers on hosts no shop has: a retired code still serves a
// working login page (pos-bmd survived the shop being re-coded to its fiscal
// number), and so does any typo or probe. Refuse them outright rather than invite
// a cashier to type a password into a subdomain that leads nowhere.
//
// The registry is a cached singleton, so this is a dictionary lookup, not a query.
// It runs before authentication: a dead host serves nothing at all, not even the
// login form or a static asset.
app.Use(async (ctx, next) =>
{
    var hosts = ctx.RequestServices.GetRequiredService<BusinessHostResolver>();
    var code = hosts.CodeFromHost(ctx.Request.Host.Value);
    if (code is not null)
    {
        var registry = ctx.RequestServices.GetRequiredService<BusinessRegistry>();
        if (await registry.GetActiveByCodeAsync(code) is null)
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            ctx.Response.ContentType = "text/plain; charset=utf-8";
            await ctx.Response.WriteAsync("Ky biznes nuk ekziston.");
            return;
        }
    }
    await next();
});

// Explicit, and it has to be: the shop-domain middleware above rewrites "/" to "/dyqani",
// and a rewrite is only meaningful before the endpoint is selected. Left implicit, ASP.NET
// puts routing at the top of the pipeline and the rewrite comes too late to matter.
app.UseRouting();

app.UseAuthentication();

// Defence in depth: a session must not be usable on a business subdomain other
// than its own. The auth cookie is host-only (no Domain is set), so a cookie
// minted on pos-bmd never travels to pos-enisi in the first place — but if that
// ever changes, or a business is re-coded under a live session, this refuses to
// serve one shop's session on another shop's host and sends it back to log in.
// Runs before authorization so a mismatch never reaches a page or a query.
app.Use(async (ctx, next) =>
{
    var raw = ctx.User.FindFirst(AuthService.BusinessIdClaim)?.Value;
    if (int.TryParse(raw, out var sessionBizId))
    {
        var resolver = ctx.RequestServices.GetRequiredService<BusinessHostResolver>();
        var hostCode = resolver.CodeFromHost(ctx.Request.Host.Value);
        if (hostCode is not null)
        {
            var registry = ctx.RequestServices.GetRequiredService<BusinessRegistry>();
            var hostBiz = await registry.GetActiveByCodeAsync(hostCode);
            if (hostBiz is null || hostBiz.Id != sessionBizId)
            {
                await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                ctx.Response.Redirect("/login");
                return;
            }
        }
    }
    await next();
});

app.UseAuthorization();

// A session whose business was deactivated (or deleted) holds a cookie naming a
// business the registry no longer serves. The DbContext factory refuses to open a
// database for it — correctly — but an unhandled exception would show the cashier
// a 500. Turn it into what it actually is: a session that is no longer valid.
app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (NoBusinessInScopeException) when (!ctx.Response.HasStarted)
    {
        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        ctx.Response.Redirect("/login");
    }
});

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// ── Shop-PC setup downloads ─────────────────────────────────────────────
// The hardware agent ships FROM the POS: a cashier PC needs no file copied to it by hand,
// it just browses to /pajisjet and runs one command. The package is staged into
// agent-package/ by tools/build-release.sh — it is a build artifact, never in git.
//
// Anonymous by design: PowerShell downloading the installer carries no auth cookie, so a
// login here would break the one-command install. Nothing in the package is a secret — it
// is the same generic agent for every shop, and its configuration (printer names, F-Link
// paths) is passed in by whoever runs it, not baked in.
var agentPackageDir = Environment.GetEnvironmentVariable("POS_AGENT_PACKAGE_DIR")
                      ?? Path.Combine(app.Environment.ContentRootPath, "agent-package");

// Only these names are servable, and each maps to a fixed file: the path never comes from
// the request, so no crafted name can walk out of the package directory.
var downloads = new Dictionary<string, (string File, string ContentType)>(StringComparer.OrdinalIgnoreCase)
{
    ["agjenti.zip"] = ("agjenti.zip", "application/zip"),
    ["instalo.ps1"] = ("instalo.ps1", "text/plain; charset=utf-8"),
    ["kiosk.ps1"]   = ("kiosk.ps1",   "text/plain; charset=utf-8"),
};

app.MapGet("/shkarko/{name}", (string name) =>
{
    if (!downloads.TryGetValue(name, out var d)) return Results.NotFound();

    var path = Path.Combine(agentPackageDir, d.File);
    return File.Exists(path)
        ? Results.File(path, d.ContentType, fileDownloadName: d.File)
        : Results.NotFound();
}).AllowAnonymous();

// The same agent, repacked for THIS business: the zip carries konfigurimi.env (this POS's
// address, this shop's printers) and a one-click instalo-ketu.cmd, so the shop PC install has
// nothing left to type and nothing left to edit — which is what made the plain zip a trap.
//
// This one is NOT anonymous, unlike the generic package above: it names the business and its
// printers. It is downloaded by a browser that is already signed in, so a cookie is all it needs.
app.MapGet("/shkarko/paketa-ime.zip", async (
    KosovaPOS.Web.Services.AgentPackageBuilder builder,
    KosovaPOS.Core.Services.BusinessProfileService profile,
    HttpContext ctx) =>
{
    var package = ctx.RequestServices.GetRequiredService<KosovaPOS.Web.Services.AgentPackage>();
    if (!package.Available) return Results.NotFound();

    var shop = await profile.GetSettingsAsync();
    var origin = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    var bytes = builder.Build(shop, origin);

    var name = string.Concat((shop?.BusinessName ?? "kosovapos")
        .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-'));

    return Results.File(bytes, "application/zip", $"agjenti-{name}.zip");
}).RequireAuthorization("perm:settings");

// ── The public shop ─────────────────────────────────────────────────────
// Plain form posts, anonymous, no JavaScript. The storefront is static-SSR: it has no
// Blazor circuit, so a customer costs the server one HTTP request per click rather than
// a live WebSocket for as long as they browse, and every page is a real HTML document a
// search engine can read.
//
// All of them are anonymous — a shop customer never signs in — and all of them resolve
// their business from the shop domain, via CurrentBusiness.

app.MapPost("/dyqani/shto", async (HttpContext ctx, KosovaPOS.Web.Services.Cart cart) =>
{
    var form = await ctx.Request.ReadFormAsync();

    if (long.TryParse(form["articleId"], out var id))
        cart.Add(id, decimal.TryParse(form["qty"], out var q) && q > 0 ? q : 1);

    // Back where they were, so adding from the grid does not throw away their scroll
    // position and adding from a product page keeps them on the product.
    var back = form["back"].ToString();
    return Results.Redirect(string.IsNullOrWhiteSpace(back) ? "/dyqani" : back);
}).AllowAnonymous().DisableAntiforgery();

app.MapPost("/dyqani/sasia", async (HttpContext ctx, KosovaPOS.Web.Services.Cart cart) =>
{
    var form = await ctx.Request.ReadFormAsync();

    if (long.TryParse(form["articleId"], out var id))
    {
        if (decimal.TryParse(form["qty"], out var q))
            cart.SetQuantity(id, q);
        else
            cart.Remove(id);
    }

    return Results.Redirect("/dyqani/shporta");
}).AllowAnonymous().DisableAntiforgery();

// Places the order. Everything the customer is charged is recomputed here from the
// database — the form carries an address, not a price.
app.MapPost("/dyqani/porosit", async (
    HttpContext ctx,
    KosovaPOS.Web.Services.Cart cart,
    ShopService shop,
    BusinessProfileService profile,
    PayPalService paypal,
    EmailService email,
    ILoggerFactory logs) =>
{
    var log = logs.CreateLogger("KosovaPOS.Shop");
    var form = await ctx.Request.ReadFormAsync();

    var settings = await profile.GetSettingsAsync();
    if (settings is null || !settings.ShopEnabled)
        return Results.Redirect("/dyqani");

    var lines = cart.Lines;
    if (lines.Count == 0)
        return Results.Redirect("/dyqani/shporta");

    var wantsPayPal = form["payment"].ToString() == "paypal";
    if (wantsPayPal && !(settings.ShopAcceptPayPal && paypal.Configured))
        return Results.Redirect("/dyqani/arka?gabim=" + Uri.EscapeDataString("Pagesa me PayPal nuk është e disponueshme."));
    if (!wantsPayPal && !settings.ShopAcceptCashOnDelivery)
        return Results.Redirect("/dyqani/arka?gabim=" + Uri.EscapeDataString("Pagesa në dorëzim nuk është e disponueshme."));

    var details = new KosovaPOS.Models.Shop.WebOrder
    {
        CustomerName = form["name"].ToString().Trim(),
        Email = form["email"].ToString().Trim(),
        Phone = form["phone"].ToString().Trim(),
        Address = form["address"].ToString().Trim(),
        City = form["city"].ToString().Trim(),
        Note = form["note"].ToString().Trim(),
        PaymentMethod = wantsPayPal
            ? KosovaPOS.Models.Shop.WebPaymentMethod.PayPal
            : KosovaPOS.Models.Shop.WebPaymentMethod.CashOnDelivery,
    };

    if (details.CustomerName.Length == 0 || details.Email.Length == 0 || details.Address.Length == 0)
        return Results.Redirect("/dyqani/arka?gabim=" + Uri.EscapeDataString("Emri, emaili dhe adresa janë të detyrueshme."));

    var placed = await shop.PlaceOrderAsync(lines, details, settings);
    if (!placed.Ok)
        return Results.Redirect("/dyqani/arka?gabim=" + Uri.EscapeDataString(placed.Error!));

    var order = placed.Order;
    var origin = $"{ctx.Request.Scheme}://{ctx.Request.Host}";

    if (!wantsPayPal)
    {
        // Cash on delivery: nothing is paid, but the shop has committed to shipping it, so
        // the goods come out of stock now.
        await shop.ConfirmAsync(order.Id, KosovaPOS.Models.Shop.WebPaymentStatus.Pending);
        cart.Clear();

        await email.SendOrderConfirmationAsync(order, settings, origin);
        await email.SendShopNotificationAsync(order, settings, origin);

        return Results.Redirect($"/dyqani/faleminderit/{order.OrderNumber}");
    }

    var created = await paypal.CreateOrderAsync(
        order,
        returnUrl: $"{origin}/dyqani/paypal/kthim",
        cancelUrl: $"{origin}/dyqani/paypal/anulo?porosia={order.OrderNumber}",
        brandName: settings.BusinessName ?? "Dyqani");

    if (created is null)
    {
        log.LogError("Could not create a PayPal order for {Order}.", order.OrderNumber);
        return Results.Redirect("/dyqani/arka?gabim=" + Uri.EscapeDataString(
            "Nuk u lidhëm dot me PayPal. Provoni sërish, ose zgjidhni pagesën në dorëzim."));
    }

    await shop.SetPayPalOrderIdAsync(order.Id, created.Id);

    // The cart is NOT cleared here. The customer has not paid yet, and they may well come
    // straight back by pressing Cancel — arriving at an empty basket after abandoning a
    // payment is how you lose a sale you already had.
    return Results.Redirect(created.ApproveUrl);
}).AllowAnonymous().DisableAntiforgery();

// PayPal sends the customer back here after they approve. Their arrival proves they
// clicked a button — not that anyone was charged — so the money is captured server-side
// and only PayPal's own COMPLETED is believed.
app.MapGet("/dyqani/paypal/kthim", async (
    HttpContext ctx,
    KosovaPOS.Web.Services.Cart cart,
    ShopService shop,
    BusinessProfileService profile,
    PayPalService paypal,
    EmailService email,
    ILoggerFactory logs) =>
{
    var log = logs.CreateLogger("KosovaPOS.Shop");

    var token = ctx.Request.Query["token"].ToString();   // PayPal's order id
    if (string.IsNullOrWhiteSpace(token))
        return Results.Redirect("/dyqani");

    var order = await shop.FindByPayPalOrderAsync(token);
    if (order is null)
    {
        log.LogWarning("PayPal returned with an unknown order id {Token}.", token);
        return Results.Redirect("/dyqani");
    }

    // Refreshing the return URL must not capture twice, and must not fail the second time.
    if (order.PaymentStatus == KosovaPOS.Models.Shop.WebPaymentStatus.Paid)
    {
        cart.Clear();
        return Results.Redirect($"/dyqani/faleminderit/{order.OrderNumber}");
    }

    var capture = await paypal.CaptureOrderAsync(token);
    if (!capture.Completed)
    {
        await shop.MarkFailedAsync(order.Id);
        log.LogError("PayPal capture did not complete for {Order}: {Error}", order.OrderNumber, capture.Error);
        return Results.Redirect("/dyqani/arka?gabim=" + Uri.EscapeDataString(
            "Pagesa nuk përfundoi. Nuk u tërhoq asnjë shumë. Provoni sërish."));
    }

    // Money is in. Stock comes off exactly once — ConfirmAsync is idempotent.
    await shop.ConfirmAsync(order.Id, KosovaPOS.Models.Shop.WebPaymentStatus.Paid, capture.CaptureId);
    cart.Clear();

    var settings = await profile.GetSettingsAsync();
    if (settings is not null)
    {
        var origin = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
        var paid = await shop.FindByNumberAsync(order.OrderNumber);
        if (paid is not null)
        {
            await email.SendOrderConfirmationAsync(paid, settings, origin);
            await email.SendShopNotificationAsync(paid, settings, origin);
        }
    }

    return Results.Redirect($"/dyqani/faleminderit/{order.OrderNumber}");
}).AllowAnonymous();

// The customer pressed Cancel at PayPal. Nothing was charged and no stock moved — the
// order row stays Pending as a record that someone got this far and did not finish.
app.MapGet("/dyqani/paypal/anulo", () =>
    Results.Redirect("/dyqani/arka?gabim=" + Uri.EscapeDataString("Pagesa u anulua. Shporta juaj është ende këtu.")))
    .AllowAnonymous();

// Sign-out endpoint (POST from the shell)
app.MapPost("/auth/logout", async (HttpContext ctx) =>
{
    // Audited before the sign-out: afterwards the principal is gone and there is
    // nobody left to name in the row.
    await AuditSession(ctx, AuditAction.Logout);

    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

// ── Impersonation ───────────────────────────────────────────────────────
// A platform admin assumes a business user's identity. The session becomes a
// full business principal (so every permission gate and every tenant query sees
// exactly what that user sees), stamped with who is really driving so it stays
// auditable and reversible. Only reachable with the platform policy; only the
// enter side needs it — exit downgrades and is open to any impersonating session.
app.MapPost("/admin/impersonate", async (HttpContext ctx) =>
{
    var antiforgery = ctx.RequestServices.GetRequiredService<IAntiforgery>();
    try { await antiforgery.ValidateRequestAsync(ctx); }
    catch { return Results.Redirect("/admin/bizneset"); }

    var form = await ctx.Request.ReadFormAsync();
    if (!int.TryParse(form["businessId"], out var bizId) || !int.TryParse(form["userId"], out var userId))
        return Results.Redirect("/admin/bizneset");

    var registry = ctx.RequestServices.GetRequiredService<BusinessRegistry>();
    var business = await registry.GetActiveByIdAsync(bizId);
    if (business is null)
        return Results.Redirect("/admin/bizneset");

    var auth = ctx.RequestServices.GetRequiredService<AuthService>();
    var user = await auth.FindUserAsync(business, userId);
    if (user is null || !user.IsActive)
        return Results.Redirect($"/admin/imitim/{bizId}");

    // ctx.User is the platform admin (this endpoint required the platform policy);
    // it is stamped into the new session as the impersonator.
    var principal = AuthService.BuildPrincipal(
        user, business, CookieAuthenticationDefaults.AuthenticationScheme, impersonator: ctx.User);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

    // Written to both logs, deliberately. The control database records that an
    // operator reached into a shop; the shop's own database records that it was
    // reached into — so neither the platform nor the shop holds the only copy.
    var operatorName = ctx.User.Identity?.Name ?? "?";
    var audit = ctx.RequestServices.GetRequiredService<AuditService>();
    var ip = ctx.Connection.RemoteIpAddress?.ToString();
    await audit.WritePlatformAsync(operatorName, AuditAction.ImpersonateStart, business,
        targetUser: user.Username, ip: ip, details: $"Imitim i {user.FullName} ({user.Role})");
    await audit.WriteAsync(business,
        new AuditActor(user.Id.ToString(), user.Username, operatorName, ip),
        AuditAction.ImpersonateStart, "POSUser", user.Id, user.Username,
        details: $"Operatori i platformës {operatorName} filloi imitimin.");

    var hosts = ctx.RequestServices.GetRequiredService<BusinessHostResolver>();
    return Results.Redirect(hosts.IsUnderBaseHost(ctx.Request.Host.Value)
        ? hosts.UrlForCode(business.Code)
        : "/");
}).RequireAuthorization(AuthService.PlatformPolicy);

// Leave an impersonated session and restore the platform operator, without a
// second login — the operator's identity was carried in the impersonator claims.
app.MapPost("/admin/impersonate/exit", async (HttpContext ctx) =>
{
    var antiforgery = ctx.RequestServices.GetRequiredService<IAntiforgery>();
    try { await antiforgery.ValidateRequestAsync(ctx); }
    catch { return Results.Redirect("/"); }

    var platform = AuthService.BuildPrincipalFromImpersonator(
        ctx.User, CookieAuthenticationDefaults.AuthenticationScheme);
    if (platform is null)
    {
        // Not actually impersonating — just sign out.
        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/login");
    }

    // Closes the impersonation window in both logs, so the record says how long the
    // operator was inside the shop and not merely that they went in.
    await AuditSession(ctx, AuditAction.ImpersonateEnd);

    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, platform);

    // The platform console lives on the POS apex. If exit was hit on a business
    // subdomain (the common case — you impersonate into the shop's host), send the
    // now-platform session back to the apex; a wide cookie carries it. Otherwise
    // (already on the apex, or localhost in dev) a relative path is right.
    var hosts = ctx.RequestServices.GetRequiredService<BusinessHostResolver>();
    var onBusinessHost = hosts.CodeFromHost(ctx.Request.Host.Value) is not null;
    return Results.Redirect(onBusinessHost
        ? $"https://pos.{hosts.BaseHost}/admin/bizneset"
        : "/admin/bizneset");
}).RequireAuthorization();

app.Run();

/// <summary>
/// Audits an act performed by whoever is currently signed in — a logout, or leaving
/// an impersonated session — routing it to the log(s) that principal belongs in: a
/// business user's to their shop, a platform operator's to the control database, and
/// an impersonating session's to both (it is one act by two identities).
///
/// Must be called *before* the sign-in or sign-out it describes, while ctx.User still
/// names someone.
/// </summary>
static async Task AuditSession(HttpContext ctx, string action)
{
    var user = ctx.User;
    if (user.Identity?.IsAuthenticated != true)
        return;

    var audit = ctx.RequestServices.GetRequiredService<AuditService>();
    var ip = ctx.Connection.RemoteIpAddress?.ToString();

    if (user.HasClaim(AuthService.PlatformAdminClaim, "true"))
    {
        await audit.WritePlatformAsync(user.Identity.Name ?? "?", action, ip: ip);
        return;
    }

    if (!int.TryParse(user.FindFirst(AuthService.BusinessIdClaim)?.Value, out var bizId))
        return;

    var business = await ctx.RequestServices.GetRequiredService<BusinessRegistry>().GetActiveByIdAsync(bizId);
    if (business is null)
        return;   // deactivated mid-session: there is no database left to write to

    var actor = KosovaPOS.Web.Services.Audit.ActorFrom(user, ip);
    await audit.WriteAsync(business, actor, action, "Session", details: $"{user.Identity.Name}");

    if (actor.ImpersonatedBy is not null)
    {
        await audit.WritePlatformAsync(actor.ImpersonatedBy, action, business,
            targetUser: user.Identity.Name, ip: ip);
    }
}
