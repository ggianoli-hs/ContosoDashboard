using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public class DocumentAuditEvent
{
    [Key]
    public int DocumentAuditEventId { get; set; }

    public int? DocumentIdSnapshot { get; set; }

    public int? ActorUserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    [Required]
    public DateTime OccurredUtc { get; set; }

    [Required]
    [MaxLength(50)]
    public string Outcome { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Details { get; set; }

    public virtual User? ActorUser { get; set; }
}
