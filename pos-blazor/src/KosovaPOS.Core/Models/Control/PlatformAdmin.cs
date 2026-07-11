using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.Control;

/// <summary>
/// An operator of the platform itself (you), not a user of any business. Lives
/// in the control database and can sign in only at <c>/admin/login</c>.
///
/// A platform admin holds no <c>bizid</c> claim, so the tenant-aware DbContext
/// factory refuses to open any business database for them. Reaching business
/// data is a deliberate, audited impersonation step — not a side effect of being
/// an admin.
/// </summary>
[Table("PlatformAdmins")]
public class PlatformAdmin
{
    public int Id { get; set; }

    [Required, StringLength(64)]
    public string Username { get; set; } = "";

    [Required, StringLength(256)]
    public string PasswordHash { get; set; } = "";

    [Required, StringLength(200)]
    public string FullName { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
