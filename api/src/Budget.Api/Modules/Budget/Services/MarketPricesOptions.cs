using System.ComponentModel.DataAnnotations;

namespace Budget.Api.Modules.Budget.Services;

public sealed class MarketPricesOptions
{
    public const string SectionName = "MarketPrices";

    [Range(1, 720)]
    public int CacheTtlMinutes { get; init; } = 30;

    [Range(1, 30)]
    public int ProviderTimeoutSeconds { get; init; } = 5;

    [Range(1, 32)]
    public int MaxParallelSymbolFetches { get; init; } = 4;
}
