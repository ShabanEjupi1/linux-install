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
