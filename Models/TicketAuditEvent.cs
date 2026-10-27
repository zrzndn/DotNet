using System.ComponentModel.DataAnnotations;

namespace OpsHub.Models;

public class TicketAuditEvent
{
    public int Id { get; set; }

    public int TicketId { get; set; }

    public SupportTicket Ticket { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string EventType { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ActorId { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
