namespace Budget.Api.Modules.Budget.Domain.Projects;

public sealed class ProjectCostItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StepId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PlannedAmount { get; set; }
    public decimal ManualAdjustment { get; set; }
    public bool IsDone { get; set; }
    public DateTimeOffset? DoneAt { get; set; }
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ProjectStep Step { get; set; } = null!;
    public ICollection<ProjectPayment> Payments { get; set; } = [];
    public ICollection<ProjectAttachment> Attachments { get; set; } = [];
}
