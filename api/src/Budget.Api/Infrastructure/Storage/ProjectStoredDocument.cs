namespace Budget.Api.Infrastructure.Storage;

public sealed class ProjectStoredDocument
{
    public required string RelativePath { get; init; }
    public required string StoredFileName { get; init; }
    public required long SizeBytes { get; init; }
    public required string MimeType { get; init; }
}
