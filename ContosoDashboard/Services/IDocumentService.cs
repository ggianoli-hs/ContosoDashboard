using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

public sealed record DocumentUploadRequest(
    string Title,
    string? Description,
    string Category,
    int? ProjectId,
    int? TaskId,
    string FileName,
    long DeclaredSizeBytes,
    IReadOnlyCollection<string> Tags,
    Stream Content);

public sealed record DocumentUploadResult(bool Succeeded, string Message, int? DocumentId = null);

public sealed record DocumentSummary(
    int DocumentId,
    string Title,
    string Category,
    DateTime UploadedUtc,
    long SizeBytes,
    string? ProjectName,
    IReadOnlyList<string> Tags,
    string ScanState);

public interface IDocumentService
{
    Task<DocumentUploadResult> UploadAsync(
        DocumentUploadRequest request,
        int currentUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentSummary>> GetMyDocumentsAsync(
        int currentUserId,
        CancellationToken cancellationToken = default);

    Task<bool> CanReadDocumentAsync(
        int documentId,
        int currentUserId,
        CancellationToken cancellationToken = default);
}
