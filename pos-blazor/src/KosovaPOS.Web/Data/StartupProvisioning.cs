using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;
using KosovaPOS.Core.Services;

namespace KosovaPOS.Web;

/// <summary>
/// Brings the whole installation up to schema on boot: the control database, then
/// every business database.
///
/// Runs the sweep serially at startup rather than lazily on first login. With a
/// handful of shops that costs a second or two and buys a loud, early failure; if
/// this ever serves dozens of businesses it should move to a background service,
/// because container start time grows with the customer count.
/// </summary>
public static class StartupProvisioning
{
    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("KosovaPOS.Startup");
        var posFactory = sp.GetRequiredService<PosDbContextFactory>();
        var provisioner = sp.GetRequiredService<BusinessProvisioner>();
        var registry = sp.GetRequiredService<BusinessRegistry>();
        var controlFactory = sp.GetRequiredService<IDbContextFactory<ControlDbContext>>();

        // 1. The control database must exist before EF can migrate it — EF creates
        //    schema, not databases.
        await provisioner.EnsureDatabaseExistsAsync(ControlDbContext.DatabaseName);

        await using (var control = await controlFactory.CreateDbContextAsync())
        {
            await control.Database.MigrateAsync();

            var platformUser = Environment.GetEnvironmentVariable("POS_PLATFORM_ADMIN_USER") ?? "platform";
            var platformName = Environment.GetEnvironmentVariable("POS_PLATFORM_ADMIN_NAME") ?? "Platform Operator";
            var platformPassword = Environment.GetEnvironmentVariable("POS_PLATFORM_ADMIN_PASSWORD");

            var generated = await PlatformAuthService.EnsureSeedAdminAsync(
                control, platformUser, platformName, platformPassword);

            if (generated is not null)
            {
                log.LogWarning(
                    "Seeded platform admin \"{User}\" with generated password: {Password} — sign in at /admin/login and change it now.",
                    platformUser, generated);
            }
        }

        // 2. Adopt the database this app was already pointed at as business #1.
        //    No data moves; the existing shop simply acquires a login code.
        var primaryCode = Environment.GetEnvironmentVariable("POS_PRIMARY_BUSINESS_CODE") ?? "bmd";
        var adopted = await provisioner.AdoptPrimaryBusinessAsync(primaryCode, "Biznesi kryesor");

        // 3. Migrate every active business, then seed an admin into any that has no
        //    users. A broken database for one business must not stop the app from
        //    serving the others, so failures are logged and skipped rather than thrown.
        foreach (var business in await registry.ListActiveAsync())
        {
            try
            {
                await using var db = posFactory.CreateFor(business);
                await db.Database.MigrateAsync();

                var seedUser = Environment.GetEnvironmentVariable("POS_SEED_ADMIN_USER") ?? "shaban";
                var seedName = Environment.GetEnvironmentVariable("POS_SEED_ADMIN_NAME") ?? "Shaban Ejupi";
                var seedPassword = Environment.GetEnvironmentVariable("POS_SEED_ADMIN_PASSWORD");

                // Only the primary business inherits the env-configured seed identity.
                // Businesses created through the console seed their own admin, and an
                // empty user table there means something went wrong — not that this
                // installation's operator should be granted an account in it.
                if (business.DatabaseName == posFactory.PrimaryDatabaseName)
                {
                    var generated = await PosSeeder.EnsureAdminAsync(db, seedUser, seedName, seedPassword);
                    if (generated is not null)
                    {
                        log.LogWarning(
                            "Seeded admin \"{User}\" in {Database} with generated password: {Password} — change it now.",
                            seedUser, business.DatabaseName, generated);
                    }
                }

                log.LogInformation("Business '{Code}' ({Database}) is up to schema.", business.Code, business.DatabaseName);
            }
            catch (Exception e)
            {
                log.LogError(e, "Failed to migrate business '{Code}' ({Database}). It will not work until this is fixed.",
                    business.Code, business.DatabaseName);
            }
        }

        if (adopted is not null)
        {
            log.LogWarning("First multi-business boot: existing database '{Database}' registered as business code '{Code}'. " +
                           "Users must now enter that code on the login form.",
                adopted.DatabaseName, adopted.Code);
        }
    }
}
