namespace Budget.Api.Modules.Budget.Domain;

public sealed class InvestmentHolding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal Units { get; set; }
    public decimal AverageCost { get; set; }
    public decimal? ManualPriceOverride { get; set; }
    public decimal LastFetchedPrice { get; set; }
    public DateTimeOffset LastPriceUpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public BudgetAccount Account { get; set; } = null!;
}
