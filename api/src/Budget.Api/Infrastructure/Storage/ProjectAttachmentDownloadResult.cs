namespace Budget.Api.Infrastructure.Storage;

public sealed class ProjectAttachmentDownloadResult
{
    public required Stream Stream { get; init; }
    public required string ContentType { get; init; }
    public required string FileDownloadName { get; init; }
}
