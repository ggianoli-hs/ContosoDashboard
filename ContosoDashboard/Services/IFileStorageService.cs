namespace ContosoDashboard.Services;

public sealed record StagedFile(string RelativePath, long SizeBytes);

public interface IFileStorageService
{
    Task<StagedFile> StageAsync(Stream content, long maximumBytes, CancellationToken cancellationToken = default);
    Task<string> PromoteAsync(string stagedRelativePath, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string generatedRelativePath, CancellationToken cancellationToken = default);
    Task DeleteAsync(string generatedRelativePath, CancellationToken cancellationToken = default);
}
