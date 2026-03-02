namespace Budget.Api.Modules.Budget.Domain;

public sealed class AnnualPlanCell
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal PlannedAmount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid CategoryId { get; set; }
    public BudgetCategory Category { get; set; } = null!;
}
