namespace Budget.Api.Modules.Budget.Domain.Projects;

public sealed class ProjectPayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ItemId { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset PaymentDate { get; set; } = DateTimeOffset.UtcNow;
    public string Note { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ProjectCostItem Item { get; set; } = null!;
    public ICollection<ProjectAttachment> Attachments { get; set; } = [];
}
