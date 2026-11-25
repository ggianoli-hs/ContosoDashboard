using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public class Document
{
    [Key]
    public int DocumentId { get; set; }

    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    public int? ProjectId { get; set; }

    [Required]
    public int UploaderId { get; set; }

    [Required]
    public DateTime UploadedUtc { get; set; }

    [Required]
    public long SizeBytes { get; set; }

    [Required]
    [MaxLength(255)]
    public string ContentType { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(1024)]
    public string RelativeStoragePath { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string ScanState { get; set; } = DocumentScanStates.Unscanned;

    public virtual User Uploader { get; set; } = null!;
    public virtual Project? Project { get; set; }
    public virtual ICollection<DocumentTag> Tags { get; set; } = new List<DocumentTag>();
    public virtual ICollection<DocumentTask> DocumentTasks { get; set; } = new List<DocumentTask>();
    public virtual ICollection<DocumentShare> Shares { get; set; } = new List<DocumentShare>();
}

public static class DocumentScanStates
{
    public const string Unscanned = "Unscanned";
    public const string PendingScan = "PendingScan";
    public const string Clean = "Clean";
    public const string Rejected = "Rejected";
    public const string ScanFailed = "ScanFailed";
}

public static class DocumentCategories
{
    public static readonly string[] All =
    [
        "Project Documents",
        "Team Resources",
        "Personal Files",
        "Reports",
        "Presentations",
        "Other"
    ];
}
