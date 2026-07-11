using Microsoft.EntityFrameworkCore;
using KosovaPOS.Core.Data;

namespace KosovaPOS.Web.Services;

/// <summary>
/// The seam that makes the whole application multi-business without any of it
/// knowing. Every domain service injects <c>IDbContextFactory&lt;PosDbContext&gt;</c>
/// and calls <c>CreateDbContextAsync()</c>; this implementation answers with a
/// context connected to the signed-in user's database.
///
/// Registered scoped, replacing the singleton that <c>AddDbContextFactory</c>
/// would install — a singleton could not see the user, and in Blazor Server a
/// scope is the circuit, which belongs to exactly one signed-in user.
///
/// It has no default and no fallback: a scope with no business gets an exception,
/// not the primary database. That property is the reason a query which forgets to
/// filter by business still cannot read another business's rows.
/// </summary>
public sealed class TenantDbContextFactory : IDbContextFactory<PosDbContext>
{
    private readonly CurrentBusiness _current;
    private readonly PosDbContextFactory _factory;

    public TenantDbContextFactory(CurrentBusiness current, PosDbContextFactory factory)
    {
        _current = current;
        _factory = factory;
    }

    /// <summary>
    /// Not supported: resolving the business requires reading the authentication
    /// state, which is async. Nothing in this codebase calls the sync overload, and
    /// blocking on the async path here would deadlock a Blazor circuit. If you need
    /// a context without a signed-in user, inject <see cref="PosDbContextFactory"/>
    /// and name the business explicitly.
    /// </summary>
    public PosDbContext CreateDbContext() =>
        throw new NotSupportedException(
            "Use CreateDbContextAsync(): the business database is resolved from the signed-in user.");

    public async Task<PosDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        var business = await _current.RequireAsync();
        return _factory.CreateFor(business);
    }
}
