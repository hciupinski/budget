namespace Budget.Api.Modules.Budget.Services;

public sealed record MarketPriceLookupResult(
    string Symbol,
    decimal? Price,
    DateTimeOffset? FetchedAtUtc,
    bool FromCache,
    bool ProviderFailed);
