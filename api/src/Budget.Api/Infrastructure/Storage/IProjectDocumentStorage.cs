using Microsoft.AspNetCore.Http;

namespace Budget.Api.Infrastructure.Storage;

public interface IProjectDocumentStorage
{
    Task<ProjectStoredDocument> SaveAsync(
        IFormFile file,
        Guid projectId,
        Guid itemId,
        CancellationToken cancellationToken);

    Task<string> MarkAsDeletedAsync(string relativePath, CancellationToken cancellationToken);

    Task<string> MoveToProjectRootDeletedAsync(
        Guid projectId,
        string relativePath,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken);
}
