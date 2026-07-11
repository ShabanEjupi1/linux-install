using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using KosovaPOS.Models.Control;

namespace KosovaPOS.Core.Data;

/// <summary>
/// Opens a <see cref="PosDbContext"/> against a *named* database. Every business
/// database shares one Postgres server and one credential: the connection string
/// is the configured template with <c>Database=</c> swapped, so no per-business
/// password is ever stored in the control database.
///
/// This is the explicit, business-is-an-argument factory. Request-scoped code
/// should not use it — it should inject <c>IDbContextFactory&lt;PosDbContext&gt;</c>,
/// which resolves the business from the signed-in user. This one exists for the
/// three places that legitimately have no ambient business: login (which must
/// name the business before anyone is signed in), provisioning, and startup.
/// </summary>
public sealed class PosDbContextFactory
{
    private readonly string _template;

    // DbContextOptions is expensive to build and safe to share. Npgsql excludes
    // the connection string from the options' service-provider cache key, so
    // rebuilding per call would not thrash EF's internal provider — but it would
    // still re-run the whole extension pipeline on every query. Cache per database.
    private readonly ConcurrentDictionary<string, DbContextOptions<PosDbContext>> _options = new(StringComparer.Ordinal);

    public PosDbContextFactory(string templateConnectionString)
    {
        if (string.IsNullOrWhiteSpace(templateConnectionString))
            throw new ArgumentException("Connection template is required.", nameof(templateConnectionString));
        _template = templateConnectionString;
    }

    /// <summary>The database named by the configured template — the primary business.</summary>
    public string PrimaryDatabaseName =>
        new NpgsqlConnectionStringBuilder(_template).Database
        ?? throw new InvalidOperationException("Connection template names no database.");

    /// <summary>
    /// Connection to the <c>postgres</c> maintenance database. CREATE DATABASE
    /// cannot run inside the database it creates, nor inside a transaction.
    /// </summary>
    public string MaintenanceConnectionString => ForDatabase("postgres");

    public string ControlConnectionString => ForDatabase(ControlDbContext.DatabaseName);

    /// <summary>
    /// The template with <c>Database=</c> replaced. Validates the name even though
    /// it lands in a connection string rather than in DDL: a name that fails here
    /// would fail in <see cref="PgIdentifier.RequireDatabaseName"/> at CREATE time
    /// anyway, and rejecting it early keeps the two paths honest about the same rule.
    /// </summary>
    public string ForDatabase(string databaseName)
    {
        // "postgres" is a valid identifier; RequireDatabaseName accepts it.
        PgIdentifier.RequireDatabaseName(databaseName);
        return new NpgsqlConnectionStringBuilder(_template) { Database = databaseName }.ConnectionString;
    }

    public DbContextOptions<PosDbContext> OptionsFor(string databaseName) =>
        _options.GetOrAdd(PgIdentifier.RequireDatabaseName(databaseName), static (db, self) =>
            new DbContextOptionsBuilder<PosDbContext>()
                .UseNpgsql(self.ForDatabase(db))
                .Options, this);

    /// <summary>Caller owns the returned context and must dispose it.</summary>
    public PosDbContext CreateFor(string databaseName) => new(OptionsFor(databaseName));

    public PosDbContext CreateFor(Business business) => CreateFor(business.DatabaseName);
}
