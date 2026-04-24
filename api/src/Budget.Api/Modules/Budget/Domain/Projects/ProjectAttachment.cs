namespace Budget.Api.Modules.Budget.Domain.Projects;

public sealed class ProjectAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ItemId { get; set; }
    public Guid? PaymentId { get; set; }
    public ProjectAttachmentKind Kind { get; set; } = ProjectAttachmentKind.Document;
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string OriginalName { get; set; } = string.Empty;
    public string StoredRelativePath { get; set; } = string.Empty;
    public bool IsRemoved { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
    public string? RemovedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ProjectCostItem Item { get; set; } = null!;
    public ProjectPayment? Payment { get; set; }
}
