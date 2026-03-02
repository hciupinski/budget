namespace Budget.Api.Modules.Budget.Domain;

public sealed class BudgetCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public BudgetSection Section { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<AnnualPlanCell> AnnualPlanCells { get; set; } = [];
    public List<MonthlyAction> MonthlyActions { get; set; } = [];
}
