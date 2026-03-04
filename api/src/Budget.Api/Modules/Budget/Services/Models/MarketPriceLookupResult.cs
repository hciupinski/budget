namespace Budget.Api.Modules.Budget.Services;

public sealed record MarketPriceLookupResult(
    string Symbol,
    decimal? CurrentClosePrice,
    DateTimeOffset? CurrentCloseAt,
    decimal? PreviousClosePrice,
    DateTimeOffset? PreviousCloseAt,
    bool FromCache,
    bool ProviderFailed);
