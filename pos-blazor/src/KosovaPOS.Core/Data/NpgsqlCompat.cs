namespace KosovaPOS.Core.Data;

/// <summary>
/// The desktop models use <c>DateTime.Now</c>/<c>Today</c> (Kind=Local) as
/// wall-clock values, not timezone-aware instants. Npgsql's legacy timestamp
/// behaviour maps <c>DateTime</c> to <c>timestamp without time zone</c> and
/// accepts Local/Unspecified kinds, so the ported services work unchanged.
///
/// This is a *process-global* AppContext switch read when an Npgsql data source
/// is built. It used to be set from <see cref="PosDbContext"/>'s static
/// constructor, which meant it was only set once something touched that type —
/// so the moment startup opened <see cref="ControlDbContext"/> first, the switch
/// was still off and writing a Kind=Local DateTime threw. A global that turns on
/// depending on which type you happen to load first is not a thing to leave lying
/// around, so every entry point calls this explicitly instead: the web host, the
/// design-time factories, and the static constructor that used to own it.
/// </summary>
public static class NpgsqlCompat
{
    /// <summary>
    /// Idempotent. Must run before the first Npgsql data source is built —
    /// that is, before any DbContext is used.
    /// </summary>
    public static void EnableLegacyTimestampBehavior() =>
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
}
