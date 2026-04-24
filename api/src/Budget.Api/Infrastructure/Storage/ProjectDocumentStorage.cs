using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Budget.Api.Infrastructure.Storage;

public sealed class ProjectDocumentStorage(
    IOptions<ProjectDocumentsOptions> options,
    ILogger<ProjectDocumentStorage> logger)
    : IProjectDocumentStorage
{
    private readonly string _rootPath = Path.GetFullPath(options.Value.RootPath.Trim());

    public async Task<ProjectStoredDocument> SaveAsync(
        IFormFile file,
        Guid projectId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var extension = ResolveExtension(file.FileName, file.ContentType);
        var folder = Path.Combine(_rootPath, projectId.ToString("N"), itemId.ToString("N"), DateTime.UtcNow.ToString("yyyyMMdd"));
        Directory.CreateDirectory(folder);

        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(folder, storedFileName);

        await using (var target = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true))
        {
            await file.CopyToAsync(target, cancellationToken);
        }

        var relativePath = Path.GetRelativePath(_rootPath, fullPath)
            .Replace('\\', '/');

        logger.LogInformation("Stored project document {RelativePath}", relativePath);

        return new ProjectStoredDocument
        {
            RelativePath = relativePath,
            StoredFileName = storedFileName,
            SizeBytes = file.Length,
            MimeType = file.ContentType
        };
    }

    public Task<string> MarkAsDeletedAsync(string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sanitized = NormalizeRelativePath(relativePath);
        var sourcePath = ResolveSafePath(sanitized);
        if (!File.Exists(sourcePath))
        {
            return Task.FromResult(sanitized);
        }

        var fileName = Path.GetFileName(sourcePath);
        if (fileName.StartsWith("xdel_", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(sanitized);
        }

        var directory = Path.GetDirectoryName(sourcePath) ?? _rootPath;
        var targetPath = CreateUniqueDeletedPath(directory, fileName);
        File.Move(sourcePath, targetPath);

        var newRelative = Path.GetRelativePath(_rootPath, targetPath).Replace('\\', '/');
        return Task.FromResult(newRelative);
    }

    public Task<string> MoveToProjectRootDeletedAsync(
        Guid projectId,
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sanitized = NormalizeRelativePath(relativePath);
        var sourcePath = ResolveSafePath(sanitized);
        if (!File.Exists(sourcePath))
        {
            return Task.FromResult(sanitized);
        }

        var projectRootDirectory = Path.Combine(_rootPath, projectId.ToString("N"));
        Directory.CreateDirectory(projectRootDirectory);

        var fileName = Path.GetFileName(sourcePath);
        var deletedFileName = EnsureDeletedPrefix(fileName);
        var currentDirectory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        if (string.Equals(currentDirectory, projectRootDirectory, StringComparison.Ordinal) &&
            string.Equals(fileName, deletedFileName, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(sanitized);
        }

        var targetPath = Path.Combine(projectRootDirectory, deletedFileName);
        if (File.Exists(targetPath))
        {
            targetPath = CreateUniqueDeletedPath(projectRootDirectory, fileName);
        }

        File.Move(sourcePath, targetPath);
        var newRelative = Path.GetRelativePath(_rootPath, targetPath).Replace('\\', '/');
        return Task.FromResult(newRelative);
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var safePath = ResolveSafePath(NormalizeRelativePath(relativePath));
        if (!File.Exists(safePath))
        {
            throw new FileNotFoundException("Document file was not found.", safePath);
        }

        Stream stream = new FileStream(safePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
        return Task.FromResult(stream);
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        var normalized = (relativePath ?? string.Empty).Trim().Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Stored document path is empty.");
        }

        return normalized;
    }

    private string ResolveSafePath(string relativePath)
    {
        var combined = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        if (!combined.StartsWith(_rootPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Document path is outside configured storage root.");
        }

        return combined;
    }

    private static string ResolveExtension(string originalName, string contentType)
    {
        var extension = Path.GetExtension(originalName)?.Trim();
        if (!string.IsNullOrWhiteSpace(extension) && extension!.Length <= 10)
        {
            return extension.StartsWith('.') ? extension : $".{extension}";
        }

        return contentType.ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".bin"
        };
    }

    private static string EnsureDeletedPrefix(string fileName)
    {
        if (fileName.StartsWith("xdel_", StringComparison.OrdinalIgnoreCase))
        {
            return fileName;
        }

        return $"xdel_{fileName}";
    }

    private static string CreateUniqueDeletedPath(string directory, string originalFileName)
    {
        var preferredName = EnsureDeletedPrefix(originalFileName);
        var preferredPath = Path.Combine(directory, preferredName);
        if (!File.Exists(preferredPath))
        {
            return preferredPath;
        }

        var uniqueName = $"xdel_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}_{originalFileName}";
        return Path.Combine(directory, uniqueName);
    }
}
