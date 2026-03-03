namespace Budget.Api.Modules.Budget.Domain;

public sealed class BudgetUiStateEntry
{
    public string StateKey { get; set; } = string.Empty;
    public string Value { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
