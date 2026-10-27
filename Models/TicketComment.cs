using System.ComponentModel.DataAnnotations;

namespace OpsHub.Models;

public class TicketComment
{
    public int Id { get; set; }

    public int TicketId { get; set; }

    public SupportTicket Ticket { get; set; } = null!;

    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ActorId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
