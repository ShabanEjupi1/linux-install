using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using KosovaPOS.Core.Data;
using KosovaPOS.Models.Control;

namespace KosovaPOS.Core.Services;

/// <summary>The outcome of provisioning, including anything the operator must write down.</summary>
/// <param name="Business">The registered business.</param>
/// <param name="GeneratedAdminPassword">
/// Non-null only when the provisioner invented the password. Shown once and never again.
/// </param>
/// <param name="AdoptedExistingDatabase">
/// True when the database already existed and was migrated into rather than created.
/// Surfaced because it is the one outcome an operator might not have intended.
/// </param>
public sealed record ProvisionResult(Business Business, string? GeneratedAdminPassword, bool AdoptedExistingDatabase);

/// <summary>
/// Creates a business: its Postgres database, its schema, its first Admin, and
/// its row in the control registry.
///
/// Deliberately never drops a database. A failure part-way through leaves an
/// unregistered database behind, and re-running with the same code picks it up
/// and finishes the job. That is a leaked database; the alternative — rolling
/// back by dropping — is a bug away from deleting a live shop's data, and the
/// two failure modes are not remotely equally bad.
/// </summary>
public sealed class BusinessProvisioner
{
    private readonly PosDbContextFactory _pos;
    private readonly IDbContextFactory<ControlDbContext> _controlFactory;
    private readonly BusinessRegistry _registry;
    private readonly ILogger<BusinessProvisioner> _log;

    public BusinessProvisioner(
        PosDbContextFactory pos,
        IDbContextFactory<ControlDbContext> controlFactory,
        BusinessRegistry registry,
        ILogger<BusinessProvisioner> log)
    {
        _pos = pos;
        _controlFactory = controlFactory;
        _registry = registry;
        _log = log;
    }

    /// <summary>
    /// CREATE DATABASE if it isn't there. Returns true when this call created it.
    /// Runs against the <c>postgres</c> maintenance database because CREATE DATABASE
    /// runs neither inside the target database nor inside a transaction.
    /// </summary>
    public async Task<bool> EnsureDatabaseExistsAsync(string databaseName, CancellationToken ct = default)
    {
        PgIdentifier.RequireDatabaseName(databaseName);

        await using var conn = new NpgsqlConnection(_pos.MaintenanceConnectionString);
        await conn.OpenAsync(ct);

        await using (var check = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @n", conn))
        {
            check.Parameters.AddWithValue("n", databaseName);
            if (await check.ExecuteScalarAsync(ct) is not null)
                return false;
        }

        try
        {
            // The name is an identifier, not a value, so it cannot be a parameter.
            // RequireDatabaseName above is what makes this concatenation safe.
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", conn);
            await create.ExecuteNonQueryAsync(ct);
            _log.LogInformation("Created database {Database}.", databaseName);
            return true;
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.DuplicateDatabase)
        {
            // Another node won the race between our check and our CREATE.
            return false;
        }
    }

    /// <summary>Applies pending EF migrations to one business database.</summary>
    public async Task MigrateBusinessAsync(Business business, CancellationToken ct = default)
    {
        await using var db = _pos.CreateFor(business);
        await db.Database.MigrateAsync(ct);
    }

    /// <summary>
    /// Full provisioning path for the admin console. Idempotent on the database,
    /// but refuses to overwrite an existing registry row.
    /// </summary>
    public async Task<ProvisionResult> CreateBusinessAsync(
        string code,
        string name,
        string adminUsername,
        string? adminPassword = null,
        CancellationToken ct = default)
    {
        code = PgIdentifier.RequireCode((code ?? "").Trim().ToLowerInvariant());

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Business name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(adminUsername))
            throw new ArgumentException("Admin username is required.", nameof(adminUsername));

        name = name.Trim();
        adminUsername = adminUsername.Trim();

        var databaseName = PgIdentifier.DatabaseNameForCode(code);

        // Never let a business be provisioned on top of the registry itself or the
        // primary business. RequireCode's reserved list covers "control"; this
        // catches a primary database that happens to be named pos_<something>.
        if (string.Equals(databaseName, ControlDbContext.DatabaseName, StringComparison.Ordinal) ||
            string.Equals(databaseName, _pos.PrimaryDatabaseName, StringComparison.Ordinal))
            throw new InvalidOperationException($"'{code}' resolves to a reserved database ({databaseName}).");

        await using (var control = await _controlFactory.CreateDbContextAsync(ct))
        {
            if (await control.Businesses.AnyAsync(b => b.Code == code, ct))
                throw new InvalidOperationException($"A business with code '{code}' already exists.");
            if (await control.Businesses.AnyAsync(b => b.DatabaseName == databaseName, ct))
                throw new InvalidOperationException($"Database '{databaseName}' is already registered to a business.");
        }

        var created = await EnsureDatabaseExistsAsync(databaseName, ct);
        if (!created)
            _log.LogWarning("Database {Database} already existed; adopting it for business '{Code}'.", databaseName, code);

        string? generatedPassword;
        await using (var db = _pos.CreateFor(databaseName))
        {
            await db.Database.MigrateAsync(ct);
            generatedPassword = await PosSeeder.EnsureAdminAsync(db, adminUsername, adminUsername, adminPassword, ct);
            await PosSeeder.EnsureBusinessSettingsAsync(db, name, ct);
        }

        // Registered last: an unregistered database is inert and re-runnable, while
        // a registry row pointing at a database that failed to migrate is a business
        // whose users can log in to a broken app.
        var business = new Business
        {
            Code = code,
            Name = name,
            DatabaseName = databaseName,
            IsActive = true,
            CreatedAt = DateTime.Now,
        };

        await using (var control = await _controlFactory.CreateDbContextAsync(ct))
        {
            control.Businesses.Add(business);
            await control.SaveChangesAsync(ct);
        }

        _registry.Invalidate();
        _log.LogInformation("Provisioned business '{Code}' ({Name}) in {Database}.", code, name, databaseName);

        return new ProvisionResult(business, generatedPassword, AdoptedExistingDatabase: !created);
    }

    /// <summary>
    /// Registers the database the app was already pointed at as the first business,
    /// if the registry is empty. This is what makes the multi-business rollout a
    /// no-op for the existing shop: its data never moves, it simply acquires a code.
    /// </summary>
    public async Task<Business?> AdoptPrimaryBusinessAsync(string code, string fallbackName, CancellationToken ct = default)
    {
        code = PgIdentifier.RequireCode(code.Trim().ToLowerInvariant());

        await using var control = await _controlFactory.CreateDbContextAsync(ct);
        if (await control.Businesses.AnyAsync(ct))
            return null;

        var databaseName = _pos.PrimaryDatabaseName;

        // Prefer the name the shop already gave itself in Cilësimet.
        string name = fallbackName;
        try
        {
            await using var db = _pos.CreateFor(databaseName);
            var existing = await db.BusinessSettings.AsNoTracking().FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(existing?.BusinessName))
                name = existing!.BusinessName!;
        }
        catch (Exception e)
        {
            // A fresh install has no BusinessSettings table yet — it hasn't been
            // migrated. Not fatal: the caller migrates right after adoption.
            _log.LogDebug(e, "Could not read BusinessSettings from {Database}; using fallback name.", databaseName);
        }

        var business = new Business
        {
            Code = code,
            Name = name,
            DatabaseName = databaseName,
            IsActive = true,
            CreatedAt = DateTime.Now,
        };
        control.Businesses.Add(business);
        await control.SaveChangesAsync(ct);
        _registry.Invalidate();

        _log.LogInformation("Adopted existing database {Database} as business '{Code}' ({Name}).",
            databaseName, code, name);

        return business;
    }
}
