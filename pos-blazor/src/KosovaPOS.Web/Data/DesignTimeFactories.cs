using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using KosovaPOS.Core.Data;

namespace KosovaPOS.Web.Data;

/// <summary>
/// <c>dotnet ef</c> used to build the web host and pull DbContextOptions out of
/// DI. It can't any more: <c>IDbContextFactory&lt;PosDbContext&gt;</c> is now
/// scoped and resolves its database from the signed-in user's claims, and at
/// design time there is no user. These factories give the tooling an explicit,
/// user-free way in.
///
/// Both honour POS_DB_CONNECTION so `dotnet ef database update` targets whatever
/// the app targets. For PosDbContext the database in that string is the *primary*
/// business — other businesses are migrated by the provisioner and at startup,
/// never by the CLI.
/// </summary>
internal static class DesignTimeConnection
{
    public const string LocalDevDefault =
        "Host=localhost;Port=5433;Database=kosovapos;Username=postgres;Password=pos";

    public static string Template =>
        Environment.GetEnvironmentVariable("POS_DB_CONNECTION") ?? LocalDevDefault;
}

public class PosDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PosDbContext>
{
    public PosDbContext CreateDbContext(string[] args)
    {
        // Migrations must be scaffolded under the same timestamp mapping the app
        // runs with, or the generated columns won't match what it writes.
        NpgsqlCompat.EnableLegacyTimestampBehavior();
        var factory = new PosDbContextFactory(DesignTimeConnection.Template);
        return factory.CreateFor(factory.PrimaryDatabaseName);
    }
}

public class ControlDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ControlDbContext>
{
    public ControlDbContext CreateDbContext(string[] args)
    {
        NpgsqlCompat.EnableLegacyTimestampBehavior();
        var factory = new PosDbContextFactory(DesignTimeConnection.Template);
        var options = new DbContextOptionsBuilder<ControlDbContext>()
            .UseNpgsql(factory.ControlConnectionString)
            .Options;
        return new ControlDbContext(options);
    }
}
