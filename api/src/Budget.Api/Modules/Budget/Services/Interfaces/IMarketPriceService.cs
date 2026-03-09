namespace Budget.Api.Modules.Budget.Services;

public interface IMarketPriceService
{
    int CacheTtlMinutes { get; }

    bool TryGetFreshCachedPrice(string symbol, out MarketPriceCacheSnapshot? snapshot);

    Task<MarketPriceLookupResult> GetPriceAsync(string symbol, bool forceRefresh, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, MarketPriceLookupResult>> GetPricesAsync(
        IEnumerable<string> symbols,
        bool forceRefresh,
        CancellationToken cancellationToken);
}
