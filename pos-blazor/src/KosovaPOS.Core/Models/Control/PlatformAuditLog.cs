using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.Control;

/// <summary>
/// What a platform operator did, recorded in the control database.
///
/// Deliberately separate from the per-business <see cref="KosovaPOS.Models.AuditLog"/>:
/// the acts recorded here — creating a business, deactivating one, starting an
/// impersonation — either happen outside any business database or need to survive
/// one being deleted. Impersonation writes to *both*: a row here (the operator
/// reached into a shop) and a row in that shop's own log (the shop can see it was
/// reached into). Neither side can quietly drop the other's evidence.
/// </summary>
[Table("PlatformAuditLogs")]
public class PlatformAuditLog
{
    public int Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>Platform admin username. Kept as text, not an FK: the row must outlive the account.</summary>
    [Required, StringLength(100)]
    public string Actor { get; set; } = "";

    /// <summary>LOGIN, LOGIN_FAILED, LOCKOUT, LOGOUT, IMPERSONATE_START, IMPERSONATE_END, BUSINESS_CREATE, …</summary>
    [Required, StringLength(50)]
    public string Action { get; set; } = "";

    /// <summary>The business acted on, if any. Nullable — a platform login targets no business.</summary>
    public int? BusinessId { get; set; }

    [StringLength(32)]
    public string? BusinessCode { get; set; }

    /// <summary>The business user impersonated, for the impersonation actions.</summary>
    [StringLength(100)]
    public string? TargetUser { get; set; }

    [StringLength(200)]
    public string? IpAddress { get; set; }

    [StringLength(500)]
    public string? Details { get; set; }

    public bool IsSuccess { get; set; } = true;
}
