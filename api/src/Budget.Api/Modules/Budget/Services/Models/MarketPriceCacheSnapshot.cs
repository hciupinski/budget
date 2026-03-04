namespace Budget.Api.Modules.Budget.Services;

public sealed record MarketPriceCacheSnapshot(
    decimal CurrentClosePrice,
    DateTimeOffset CurrentCloseAt,
    decimal? PreviousClosePrice,
    DateTimeOffset? PreviousCloseAt);
