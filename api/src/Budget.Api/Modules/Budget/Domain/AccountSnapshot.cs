namespace Budget.Api.Modules.Budget.Domain;

public sealed class AccountSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal PlannedBalance { get; set; }
    public decimal? ActualBalance { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public BudgetAccount Account { get; set; } = null!;
}
