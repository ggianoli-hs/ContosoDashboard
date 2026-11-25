using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services;

public sealed class DocumentService : IDocumentService
{
    public const long MaximumUploadBytes = 25L * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> AllowedContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xls"] = "application/vnd.ms-excel",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".ppt"] = "application/vnd.ms-powerpoint",
            [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            [".txt"] = "text/plain",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png"
        };

    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        ApplicationDbContext context,
        IFileStorageService fileStorage,
        ILogger<DocumentService> logger)
    {
        _context = context;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<DocumentUploadResult> UploadAsync(
        DocumentUploadRequest request,
        int currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return await RejectUploadAsync(currentUserId, "InvalidMetadata",
                "Enter the required document information and select a supported file.", cancellationToken);
        }

        if (currentUserId <= 0
            || !await _context.Users.AnyAsync(user => user.UserId == currentUserId, cancellationToken))
        {
            return await RejectUploadAsync(null, "Unauthenticated",
                "Sign in with an authorized account before uploading.", cancellationToken);
        }

        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 255)
        {
            return await RejectUploadAsync(currentUserId, "InvalidTitle",
                "Enter a title no longer than 255 characters.", cancellationToken);
        }

        var category = request.Category?.Trim();
        if (string.IsNullOrWhiteSpace(category) || !DocumentCategories.All.Contains(category, StringComparer.Ordinal))
        {
            return await RejectUploadAsync(currentUserId, "InvalidCategory",
                "Select one of the listed document categories.", cancellationToken);
        }

        var description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        if (description?.Length > 2000)
        {
            return await RejectUploadAsync(currentUserId, "InvalidDescription",
                "The description must be no longer than 2,000 characters.", cancellationToken);
        }

        var displayFileName = GetSafeDisplayFileName(request.FileName);
        if (string.IsNullOrWhiteSpace(displayFileName) || displayFileName.Length > 255)
        {
            return await RejectUploadAsync(currentUserId, "InvalidFileName",
                "The selected file must have a valid name no longer than 255 characters.", cancellationToken);
        }

        var extension = Path.GetExtension(displayFileName);
        if (!AllowedContentTypes.TryGetValue(extension, out var contentType))
        {
            return await RejectUploadAsync(currentUserId, "UnsupportedExtension",
                "Choose a PDF, Office document, text file, JPEG, or PNG file.", cancellationToken);
        }

        if (request.DeclaredSizeBytes <= 0)
        {
            return await RejectUploadAsync(currentUserId, "EmptyFile",
                "The selected file is empty. Choose a file containing data.", cancellationToken);
        }

        if (request.DeclaredSizeBytes > MaximumUploadBytes)
        {
            return await RejectUploadAsync(currentUserId, "FileTooLarge",
                "Each file must be no larger than 25 MiB.", cancellationToken);
        }

        var normalizedTags = NormalizeTags(request.Tags, out var invalidTag);
        if (invalidTag)
        {
            return await RejectUploadAsync(currentUserId, "InvalidTag",
                "Each tag must be no longer than 100 characters.", cancellationToken);
        }

        var association = await AuthorizeUploadAssociationAsync(
            request.ProjectId, request.TaskId, currentUserId, cancellationToken);
        if (!association.Authorized)
        {
            return await RejectUploadAsync(currentUserId, association.AuditCode,
                association.Message, cancellationToken);
        }

        StagedFile? stagedFile = null;
        string? promotedPath = null;
        try
        {
            stagedFile = await _fileStorage.StageAsync(
                request.Content, MaximumUploadBytes, cancellationToken);
            if (stagedFile.SizeBytes <= 0)
            {
                await TryDeleteAsync(stagedFile.RelativePath);
                return await RejectUploadAsync(currentUserId, "EmptyFile",
                    "The selected file is empty. Choose a file containing data.", cancellationToken);
            }

            if (stagedFile.SizeBytes != request.DeclaredSizeBytes)
            {
                await TryDeleteAsync(stagedFile.RelativePath);
                return await RejectUploadAsync(currentUserId, "FileSizeMismatch",
                    "The selected file could not be verified. Select it again and retry.", cancellationToken);
            }

            promotedPath = await _fileStorage.PromoteAsync(stagedFile.RelativePath, cancellationToken);
            var document = new Document
            {
                Title = title,
                Description = description,
                Category = category,
                ProjectId = association.ProjectId,
                UploaderId = currentUserId,
                UploadedUtc = DateTime.UtcNow,
                SizeBytes = stagedFile.SizeBytes,
                ContentType = contentType,
                OriginalFileName = displayFileName,
                RelativeStoragePath = promotedPath,
                ScanState = DocumentScanStates.Unscanned
            };
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            foreach (var tag in normalizedTags)
            {
                document.Tags.Add(new DocumentTag
                {
                    Tag = tag.DisplayTag,
                    NormalizedTag = tag.NormalizedTag
                });
            }

            if (association.TaskId.HasValue)
            {
                document.DocumentTasks.Add(new DocumentTask
                {
                    TaskId = association.TaskId.Value,
                    LinkedUtc = DateTime.UtcNow,
                    LinkedByUserId = currentUserId
                });
            }

            _context.Documents.Add(document);
            await _context.SaveChangesAsync(cancellationToken);

            _context.DocumentAuditEvents.Add(new DocumentAuditEvent
            {
                DocumentIdSnapshot = document.DocumentId,
                ActorUserId = currentUserId,
                Action = "Upload",
                OccurredUtc = DateTime.UtcNow,
                Outcome = "Succeeded",
                Details = "Training upload; malware scanning was not performed."
            });

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new DocumentUploadResult(true,
                "File uploaded successfully. Malware scanning was not performed.", document.DocumentId);
        }
        catch (InvalidDataException)
        {
            await DetachPendingUploadEntitiesAsync();
            if (stagedFile is not null)
            {
                await TryDeleteAsync(stagedFile.RelativePath);
            }

            return await RejectUploadAsync(currentUserId, "FileTooLarge",
                "Each file must be no larger than 25 MiB.", cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await DetachPendingUploadEntitiesAsync();
            if (stagedFile is not null)
            {
                await TryDeleteAsync(stagedFile.RelativePath);
            }

            if (promotedPath is not null)
            {
                await TryDeleteAsync(promotedPath);
            }

            return await RejectUploadAsync(currentUserId, "UploadCancelled",
                "The upload was cancelled and was not saved.", CancellationToken.None, "Failed");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Document upload failed for caller {UserId}.", currentUserId);
            await DetachPendingUploadEntitiesAsync();
            if (stagedFile is not null)
            {
                await TryDeleteAsync(stagedFile.RelativePath);
            }

            if (promotedPath is not null)
            {
                await TryDeleteAsync(promotedPath);
            }

            return await RejectUploadAsync(currentUserId, "UploadFailed",
                "The upload could not be completed. Check storage and database availability, then retry.",
                CancellationToken.None, "Failed");
        }
    }

    public async Task<IReadOnlyList<DocumentSummary>> GetMyDocumentsAsync(
        int currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId <= 0)
        {
            return Array.Empty<DocumentSummary>();
        }

        var documents = await _context.Documents
            .AsNoTracking()
            .Where(document =>
                document.UploaderId == currentUserId
                && (document.ScanState == DocumentScanStates.Unscanned
                    || document.ScanState == DocumentScanStates.Clean))
            .Include(document => document.Project)
            .Include(document => document.Tags)
            .OrderByDescending(document => document.UploadedUtc)
            .ToListAsync(cancellationToken);

        return documents.Select(document => new DocumentSummary(
                document.DocumentId,
                document.Title,
                document.Category,
                document.UploadedUtc,
                document.SizeBytes,
                document.Project?.Name,
                document.Tags
                    .OrderBy(tag => tag.NormalizedTag)
                    .Select(tag => tag.Tag)
                    .ToArray(),
                document.ScanState))
            .ToArray();
    }

    public Task<bool> CanReadDocumentAsync(
        int documentId,
        int currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (documentId <= 0 || currentUserId <= 0)
        {
            return Task.FromResult(false);
        }

        return _context.Documents.AnyAsync(document =>
            document.DocumentId == documentId
            && (document.ScanState == DocumentScanStates.Unscanned
                || document.ScanState == DocumentScanStates.Clean)
            && (document.UploaderId == currentUserId
                || (document.Project != null
                    && (document.Project.ProjectManagerId == currentUserId
                        || document.Project.ProjectMembers.Any(member => member.UserId == currentUserId)))
                || document.Shares.Any(share =>
                    share.IsActive
                    && (share.RecipientUserId == currentUserId
                        || (share.RecipientProjectId.HasValue
                            && share.RecipientProject != null
                            && (share.RecipientProject.ProjectManagerId == currentUserId
                                || share.RecipientProject.ProjectMembers.Any(
                                    member => member.UserId == currentUserId)))))),
            cancellationToken);
    }

    private async Task<(bool Authorized, int? ProjectId, int? TaskId, string AuditCode, string Message)>
        AuthorizeUploadAssociationAsync(
            int? requestedProjectId,
            int? requestedTaskId,
            int currentUserId,
            CancellationToken cancellationToken)
    {
        int? projectId = requestedProjectId;
        if (requestedTaskId.HasValue)
        {
            var task = await _context.Tasks
                .Include(item => item.Project)
                .ThenInclude(project => project!.ProjectMembers)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.TaskId == requestedTaskId.Value, cancellationToken);
            if (task is null)
            {
                return (false, null, null, "TaskUnavailable",
                    "You are not authorized to upload to the selected task.");
            }

            var isTaskMember = task.Project?.ProjectMembers.Any(member => member.UserId == currentUserId) == true;
            var canAccessTask = task.AssignedUserId == currentUserId
                || task.CreatedByUserId == currentUserId
                || task.Project?.ProjectManagerId == currentUserId
                || isTaskMember;
            if (!canAccessTask
                || (requestedProjectId.HasValue && requestedProjectId != task.ProjectId))
            {
                return (false, null, null, "TaskUnavailable",
                    "You are not authorized to upload to the selected task.");
            }

            projectId = task.ProjectId;
            if (projectId.HasValue && task.Project?.ProjectManagerId != currentUserId)
            {
                return (false, null, null, "ProjectUploadNotAuthorized",
                    "Only the project manager can upload documents to this project.");
            }

            return (true, projectId, task.TaskId, string.Empty, string.Empty);
        }

        if (projectId.HasValue)
        {
            var project = await _context.Projects
                .AsNoTracking()
                .Where(item => item.ProjectId == projectId.Value)
                .Select(item => new { item.ProjectId, item.ProjectManagerId })
                .FirstOrDefaultAsync(cancellationToken);
            if (project is null || project.ProjectManagerId != currentUserId)
            {
                return (false, null, null, "ProjectUploadNotAuthorized",
                    "Only the project manager can upload documents to the selected project.");
            }

            projectId = project.ProjectId;
        }

        return (true, projectId, null, string.Empty, string.Empty);
    }

    private async Task<DocumentUploadResult> RejectUploadAsync(
        int? actorUserId,
        string auditCode,
        string message,
        CancellationToken cancellationToken,
        string outcome = "Rejected")
    {
        try
        {
            var verifiedActorId = actorUserId.HasValue
                && await _context.Users.AnyAsync(user => user.UserId == actorUserId.Value, cancellationToken)
                    ? actorUserId
                    : null;
            _context.DocumentAuditEvents.Add(new DocumentAuditEvent
            {
                ActorUserId = verifiedActorId,
                Action = "UploadRejected",
                OccurredUtc = DateTime.UtcNow,
                Outcome = outcome,
                Details = auditCode
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not persist a rejected document upload audit event ({AuditCode}).",
                auditCode);
            await DetachPendingUploadEntitiesAsync();
        }

        return new DocumentUploadResult(false, message);
    }

    private static List<(string DisplayTag, string NormalizedTag)> NormalizeTags(
        IReadOnlyCollection<string>? tags,
        out bool invalidTag)
    {
        invalidTag = false;
        var normalized = new List<(string DisplayTag, string NormalizedTag)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rawTag in tags ?? Array.Empty<string>())
        {
            var displayTag = rawTag?.Trim();
            if (string.IsNullOrWhiteSpace(displayTag))
            {
                continue;
            }

            if (displayTag.Length > 100)
            {
                invalidTag = true;
                return normalized;
            }

            var normalizedTag = displayTag.ToUpperInvariant();
            if (seen.Add(normalizedTag))
            {
                normalized.Add((displayTag, normalizedTag));
            }
        }

        return normalized;
    }

    private static string GetSafeDisplayFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        var normalizedSlashes = fileName.Replace('\\', '/');
        var name = normalizedSlashes[(normalizedSlashes.LastIndexOf('/') + 1)..].Trim();
        return name.Any(char.IsControl) ? string.Empty : name;
    }

    private async Task TryDeleteAsync(string relativePath)
    {
        try
        {
            await _fileStorage.DeleteAsync(relativePath, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not remove a private document file during cleanup.");
        }
    }

    private Task DetachPendingUploadEntitiesAsync()
    {
        _context.ChangeTracker.Clear();
        return Task.CompletedTask;
    }
}
