using Microsoft.Extensions.Options;

namespace ContosoDashboard.Services;

public sealed class LocalFileStorageOptions
{
    public string? RootPath { get; set; }
}

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(
        IOptions<LocalFileStorageOptions> options,
        IWebHostEnvironment environment,
        ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;
        var configuredRoot = Environment.ExpandEnvironmentVariables(options.Value.RootPath ?? string.Empty);
        var root = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ContosoDashboard",
                "Documents")
            : configuredRoot;

        _rootPath = Path.GetFullPath(root);
        var webRoot = Path.GetFullPath(environment.WebRootPath
            ?? Path.Combine(environment.ContentRootPath, "wwwroot"));
        var relativeToWebRoot = Path.GetRelativePath(webRoot, _rootPath);
        var isInsideWebRoot = relativeToWebRoot == "."
            || (!Path.IsPathRooted(relativeToWebRoot)
                && relativeToWebRoot != ".."
                && !relativeToWebRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !relativeToWebRoot.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal));

        if (isInsideWebRoot)
        {
            throw new InvalidOperationException("Private document storage must be outside the web root.");
        }
    }

    public async Task<StagedFile> StageAsync(
        Stream content,
        long maximumBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanRead)
        {
            throw new ArgumentException("The upload stream must be readable.", nameof(content));
        }

        var relativePath = $"staging/{Guid.NewGuid():N}.tmp";
        var fullPath = ResolvePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        long bytesWritten = 0;
        try
        {
            await using var target = new FileStream(
                fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[81920];
            int bytesRead;
            while ((bytesRead = await content.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
            {
                bytesWritten = checked(bytesWritten + bytesRead);
                if (bytesWritten > maximumBytes)
                {
                    throw new InvalidDataException("The upload exceeds the allowed size.");
                }

                await target.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            await target.FlushAsync(cancellationToken);
            return new StagedFile(relativePath, bytesWritten);
        }
        catch (Exception originalException)
        {
            try
            {
                File.Delete(fullPath);
            }
            catch (Exception cleanupException)
            {
                _logger.LogError(cleanupException,
                    "Could not remove a partial private upload after a storage failure.");
            }

            _logger.LogWarning(originalException, "Private document staging failed.");
            throw;
        }
    }

    public Task<string> PromoteAsync(
        string stagedRelativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!stagedRelativePath.StartsWith("staging/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only a generated staging file can be promoted.");
        }

        var sourcePath = ResolvePath(stagedRelativePath);
        var destinationRelativePath = $"documents/{Guid.NewGuid():N}.bin";
        var destinationPath = ResolvePath(destinationRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Move(sourcePath, destinationPath);
        return Task.FromResult(destinationRelativePath);
    }

    public Task<Stream> OpenReadAsync(
        string generatedRelativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(generatedRelativePath);
        Stream stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(
        string generatedRelativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(generatedRelativePath);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string generatedRelativePath)
    {
        if (string.IsNullOrWhiteSpace(generatedRelativePath)
            || Path.IsPathRooted(generatedRelativePath)
            || generatedRelativePath.Contains('\\')
            || generatedRelativePath.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidOperationException("A generated relative storage path is required.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(
            _rootPath,
            generatedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        var relativeToRoot = Path.GetRelativePath(_rootPath, fullPath);
        if (Path.IsPathRooted(relativeToRoot)
            || relativeToRoot == ".."
            || relativeToRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relativeToRoot.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The storage path is outside the configured private root.");
        }

        return fullPath;
    }
}
