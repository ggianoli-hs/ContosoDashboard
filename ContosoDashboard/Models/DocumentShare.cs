using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public class DocumentShare
{
    [Key]
    public int DocumentShareId { get; set; }

    [Required]
    public int DocumentId { get; set; }

    [Required]
    public int SharedByUserId { get; set; }

    public int? RecipientUserId { get; set; }
    public int? RecipientProjectId { get; set; }

    [Required]
    public DateTime CreatedUtc { get; set; }

    [Required]
    public bool IsActive { get; set; } = true;

    public DateTime? RevokedUtc { get; set; }
    public int? RevokedByUserId { get; set; }

    public virtual Document Document { get; set; } = null!;
    public virtual User SharedByUser { get; set; } = null!;
    public virtual User? RecipientUser { get; set; }
    public virtual Project? RecipientProject { get; set; }
    public virtual User? RevokedByUser { get; set; }
}
