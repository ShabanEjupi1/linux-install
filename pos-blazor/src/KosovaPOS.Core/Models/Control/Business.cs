using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.Control;

/// <summary>
/// One customer business, and the name of the Postgres database that holds all
/// of its data. There is no TenantId column anywhere in <c>PosDbContext</c> and
/// there never will be: isolation comes from the database boundary, so a query
/// that forgets to filter by tenant is not a data leak — it cannot even reach
/// another business's rows.
///
/// Lives in the control database (<see cref="Data.ControlDbContext"/>), never in
/// a business database.
/// </summary>
[Table("Businesses")]
public class Business
{
    public int Id { get; set; }

    /// <summary>
    /// What the user types into "Kodi i biznesit" on the login form. Lowercase
    /// slug, unique. Also the suffix of <see cref="DatabaseName"/> for businesses
    /// created through the provisioner.
    /// </summary>
    [Required, StringLength(32)]
    public string Code { get; set; } = "";

    [Required, StringLength(200)]
    public string Name { get; set; } = "";

    /// <summary>
    /// Postgres database name. Capped at 63 chars because that is Postgres'
    /// identifier limit — a longer name is silently truncated by the server,
    /// which would make this row point at a database that isn't the one we made.
    /// </summary>
    [Required, StringLength(63)]
    public string DatabaseName { get; set; } = "";

    /// <summary>
    /// Cleared to lock a business out. <see cref="Services.BusinessRegistry"/>
    /// only caches active rows, so revoking access takes effect within the cache
    /// TTL rather than at the next login.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
