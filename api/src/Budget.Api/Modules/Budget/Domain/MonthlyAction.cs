namespace Budget.Api.Modules.Budget.Domain;

public sealed class MonthlyAction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal PlannedAmount { get; set; }
    public decimal? ActualAmount { get; set; }
    public MonthlyActionStatus Status { get; set; } = MonthlyActionStatus.Planned;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid CategoryId { get; set; }
    public BudgetCategory Category { get; set; } = null!;
}
