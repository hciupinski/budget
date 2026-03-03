namespace Budget.Api.Modules.Budget.Domain;

public sealed class AccountTransfer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }
    public decimal Amount { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTimeOffset TransferDate { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public BudgetAccount FromAccount { get; set; } = null!;
    public BudgetAccount ToAccount { get; set; } = null!;
}
