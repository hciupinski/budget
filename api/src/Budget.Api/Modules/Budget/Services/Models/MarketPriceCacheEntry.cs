namespace Budget.Api.Modules.Budget.Services;

public sealed record MarketPriceCacheEntry(decimal Price, DateTimeOffset FetchedAtUtc);
