using Microsoft.EntityFrameworkCore;
using KosovaPOS.Models.Control;

namespace KosovaPOS.Core.Data;

/// <summary>
/// The control database (<c>pos_control</c>): the registry of businesses and the
/// platform admins who manage them. It holds no business data — no articles, no
/// receipts, no POS users. Those live in one database per business, reachable
/// only through <see cref="PosDbContextFactory"/>.
///
/// Kept as its own context, with its own migration history, so a schema change
/// to the POS never touches the registry and vice versa.
/// </summary>
public class ControlDbContext : DbContext
{
    public const string DatabaseName = "pos_control";

    static ControlDbContext()
    {
        // This context is the first one startup opens, so it — not PosDbContext —
        // is what determines whether the legacy timestamp switch was set before the
        // first Npgsql data source was built.
        NpgsqlCompat.EnableLegacyTimestampBehavior();
    }

    public ControlDbContext(DbContextOptions<ControlDbContext> options) : base(options) { }

    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<PlatformAdmin> PlatformAdmins => Set<PlatformAdmin>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Unique on both: Code is what the login form resolves, DatabaseName is
        // what we open. Two rows sharing either would silently cross-wire two
        // businesses, which is the one failure this whole design exists to prevent.
        b.Entity<Business>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Business>().HasIndex(x => x.DatabaseName).IsUnique();

        b.Entity<PlatformAdmin>().HasIndex(x => x.Username).IsUnique();
    }
}
