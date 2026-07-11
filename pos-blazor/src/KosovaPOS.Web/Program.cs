using KosovaPOS.Web;
using KosovaPOS.Web.Components;
using KosovaPOS.Web.Services;
using KosovaPOS.Core.Data;
using KosovaPOS.Core.Services;
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
builder.Services.AddScoped<KosovaPOS.Web.Services.HardwareBridge>();

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

// Sign-out endpoint (POST from the shell)
app.MapPost("/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

app.Run();
