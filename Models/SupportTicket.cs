
using System.ComponentModel.DataAnnotations;

namespace OpsHub.Models;

public class SupportTicket
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = "Hardware";

    [Required]
    [MaxLength(150)]
    public string Requester { get; set; } = string.Empty;

    [MaxLength(150)]
    public string AssignedTechnician { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Priority { get; set; } = "Medium";

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = "Open";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? DueAtUtc { get; set; }

    public DateTime? SlaDueAtUtc { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    public bool? SlaMet { get; set; }

    [MaxLength(2000)]
    public string ResolutionNotes { get; set; } = string.Empty;

    [MaxLength(450)]
    public string? OwnerUserId { get; set; }

    public bool IsArchived { get; set; }

    public DateTime? ArchivedAtUtc { get; set; }

    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();

    public ICollection<TicketAuditEvent> AuditEvents { get; set; } = new List<TicketAuditEvent>();
}
