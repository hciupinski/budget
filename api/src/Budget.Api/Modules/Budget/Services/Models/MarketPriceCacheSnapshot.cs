namespace Budget.Api.Modules.Budget.Services;

public sealed record MarketPriceCacheSnapshot(decimal Price, DateTimeOffset FetchedAtUtc);
