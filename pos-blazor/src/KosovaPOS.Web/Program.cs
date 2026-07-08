using KosovaPOS.Web.Components;
using KosovaPOS.Web.Services;
using KosovaPOS.Core.Data;
using KosovaPOS.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── EF Core / Postgres ──────────────────────────────────────────────────
// Connection resolves from (in order): env POS_DB_CONNECTION, config
// "ConnectionStrings:Postgres", then a local-dev default.
var connectionString =
    Environment.GetEnvironmentVariable("POS_DB_CONNECTION")
    ?? builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5433;Database=kosovapos;Username=postgres;Password=pos";

builder.Services.AddDbContextFactory<PosDbContext>(options =>
    options.UseNpgsql(connectionString));

// ── Domain services (from Core) ─────────────────────────────────────────
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TenantService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<SalesService>();
builder.Services.AddScoped<PurchaseService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<PartnerService>();
builder.Services.AddScoped<ShiftService>();
builder.Services.AddScoped<ReturnService>();
builder.Services.AddScoped<FinanceService>();
builder.Services.AddScoped<KosovaPOS.Web.Services.HardwareBridge>();

// ── Authentication (cookie) ─────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "KosovaPOS.Auth";
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
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

// ── Provision the database on startup ───────────────────────────────────
// Apply pending EF migrations (idempotent) so a fresh container self-creates
// its schema, then seed a single admin/admin user if the POSUsers table is
// empty (first run only). Gate with POS_SKIP_DB_INIT=true to opt out.
if (!string.Equals(Environment.GetEnvironmentVariable("POS_SKIP_DB_INIT"), "true",
        StringComparison.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PosDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await db.Database.MigrateAsync();

    if (!await db.POSUsers.AnyAsync())
    {
        db.POSUsers.Add(new KosovaPOS.Models.BMDData.POSUser
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"),
            FullName = "Administrator",
            Role = "Admin",
            IsActive = true,
            CanManageArticles = true,
            CanManagePurchases = true,
            CanManageUsers = true,
            CanViewReports = true,
            CanModifyPrices = true,
            CanDeleteReceipts = true,
            CanGiveDiscounts = true,
        });
        await db.SaveChangesAsync();
    }
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
app.UseAuthorization();
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
