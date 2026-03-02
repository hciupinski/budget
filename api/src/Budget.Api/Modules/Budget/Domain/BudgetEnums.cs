namespace Budget.Api.Modules.Budget.Domain;

public enum BudgetSection
{
    Income = 1,
    Costs = 2,
    SavingsInvestments = 3
}

public enum MonthlyActionStatus
{
    Planned = 1,
    Done = 2,
    Partial = 3,
    Skipped = 4
}
