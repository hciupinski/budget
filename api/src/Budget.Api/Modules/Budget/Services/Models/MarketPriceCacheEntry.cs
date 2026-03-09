namespace Budget.Api.Modules.Budget.Services;

public sealed record MarketPriceCacheEntry(
    decimal CurrentClosePrice,
    DateTimeOffset CurrentCloseAt,
    DateTimeOffset FetchedAtUtc,
    decimal? PreviousClosePrice,
    DateTimeOffset? PreviousCloseAt);
