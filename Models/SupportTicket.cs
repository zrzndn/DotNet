
using System.ComponentModel.DataAnnotations;

namespace OpsHub.Models;

public class SupportTicket
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = "Hardware";

    [Required]
    [MaxLength(20)]
    public string Priority { get; set; } = "Medium";

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = "Open";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
