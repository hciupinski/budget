namespace Budget.Api.Modules.Budget.Domain.Projects;

public sealed class ProjectMilestone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public ProjectCompletionStatus CompletionStatus { get; set; } = ProjectCompletionStatus.Active;
    public ProjectCompletionSource CompletionSource { get; set; } = ProjectCompletionSource.Auto;
    public DateTimeOffset? CompletedAt { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public BudgetProject Project { get; set; } = null!;
    public ICollection<ProjectStep> Steps { get; set; } = [];
}
