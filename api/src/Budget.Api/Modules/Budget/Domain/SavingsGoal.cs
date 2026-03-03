namespace Budget.Api.Modules.Budget.Domain;

public sealed class SavingsGoal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public Guid? AccountId { get; set; }
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public decimal MonthlyContributionTarget { get; set; }
    public int? TargetYear { get; set; }
    public int? TargetMonth { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public BudgetAccount? Account { get; set; }
}
