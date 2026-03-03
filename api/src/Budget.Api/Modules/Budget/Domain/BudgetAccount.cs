namespace Budget.Api.Modules.Budget.Domain;

public sealed class BudgetAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public BudgetAccountKind Kind { get; set; } = BudgetAccountKind.Bank;
    public string Currency { get; set; } = "PLN";
    public decimal CurrentBalance { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<AccountTransfer> OutgoingTransfers { get; set; } = [];
    public List<AccountTransfer> IncomingTransfers { get; set; } = [];
    public List<AccountSnapshot> Snapshots { get; set; } = [];
    public List<InvestmentHolding> Holdings { get; set; } = [];
    public List<SavingsGoal> SavingsGoals { get; set; } = [];
}
