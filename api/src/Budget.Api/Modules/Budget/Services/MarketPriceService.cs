using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Budget.Api.Modules.Budget.Services;

public sealed class MarketPriceService(
    IHttpClientFactory httpClientFactory,
    IMemoryCache memoryCache,
    IOptions<MarketPricesOptions> options,
    ILogger<MarketPriceService> logger)
    : IMarketPriceService
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> symbolLocks = new(StringComparer.OrdinalIgnoreCase);

    public int CacheTtlMinutes => options.Value.CacheTtlMinutes;

    public bool TryGetFreshCachedPrice(string symbol, out MarketPriceCacheSnapshot? snapshot)
    {
        var normalized = NormalizeSymbol(symbol);
        var cacheKey = BuildCacheKey(normalized);

        if (memoryCache.TryGetValue<MarketPriceCacheEntry>(cacheKey, out var cached) && cached is not null)
        {
            if (IsFresh(cached.FetchedAtUtc))
            {
                snapshot = new MarketPriceCacheSnapshot(cached.Price, cached.FetchedAtUtc);
                return true;
            }

            memoryCache.Remove(cacheKey);
        }

        snapshot = default;
        return false;
    }

    public async Task<MarketPriceLookupResult> GetPriceAsync(string symbol, bool forceRefresh, CancellationToken cancellationToken)
    {
        var normalized = NormalizeSymbol(symbol);

        if (!forceRefresh && TryGetFreshCachedPrice(normalized, out var cached) && cached is not null)
        {
            return new MarketPriceLookupResult(normalized, cached.Price, cached.FetchedAtUtc, FromCache: true, ProviderFailed: false);
        }

        var symbolLock = symbolLocks.GetOrAdd(normalized, _ => new SemaphoreSlim(1, 1));
        await symbolLock.WaitAsync(cancellationToken);

        try
        {
            if (!forceRefresh && TryGetFreshCachedPrice(normalized, out cached) && cached is not null)
            {
                return new MarketPriceLookupResult(normalized, cached.Price, cached.FetchedAtUtc, FromCache: true, ProviderFailed: false);
            }

            var fetchedAt = DateTimeOffset.UtcNow;
            var fetchResult = await FetchMarketPriceFromProviderAsync(normalized, cancellationToken);

            if (fetchResult.Price.HasValue)
            {
                var entry = new MarketPriceCacheEntry(fetchResult.Price.Value, fetchedAt);
                memoryCache.Set(
                    BuildCacheKey(normalized),
                    entry,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(options.Value.CacheTtlMinutes)
                    });

                return new MarketPriceLookupResult(normalized, fetchResult.Price.Value, fetchedAt, FromCache: false, ProviderFailed: false);
            }

            return new MarketPriceLookupResult(normalized, null, null, FromCache: false, ProviderFailed: fetchResult.ProviderFailed);
        }
        finally
        {
            symbolLock.Release();
        }
    }

    public async Task<IReadOnlyDictionary<string, MarketPriceLookupResult>> GetPricesAsync(
        IEnumerable<string> symbols,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var normalizedSymbols = symbols
            .Select(NormalizeSymbol)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedSymbols.Length == 0)
        {
            return new Dictionary<string, MarketPriceLookupResult>(StringComparer.OrdinalIgnoreCase);
        }

        var maxParallel = Math.Max(1, options.Value.MaxParallelSymbolFetches);
        using var semaphore = new SemaphoreSlim(maxParallel, maxParallel);

        var tasks = normalizedSymbols.Select(async symbol =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var result = await GetPriceAsync(symbol, forceRefresh, cancellationToken);
                return (Symbol: symbol, Result: result);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);

        var bySymbol = new Dictionary<string, MarketPriceLookupResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var (symbol, result) in results)
        {
            bySymbol[symbol] = result;
        }

        return bySymbol;
    }

    private async Task<(decimal? Price, bool ProviderFailed)> FetchMarketPriceFromProviderAsync(string normalizedSymbol, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("market-prices");
            client.Timeout = TimeSpan.FromSeconds(options.Value.ProviderTimeoutSeconds);
            var endpoint = $"https://stooq.com/q/l/?s={normalizedSymbol.ToLowerInvariant()}&i=d";
            var csv = await client.GetStringAsync(endpoint, cancellationToken);
            var parsed = ParseStooqClosePrice(csv);
            return (parsed, ProviderFailed: false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to fetch market price for symbol {Symbol}.", normalizedSymbol);
            return (null, ProviderFailed: true);
        }
    }

    private static decimal? ParseStooqClosePrice(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return null;
        }

        var lines = csv
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length == 0)
        {
            return null;
        }

        if (lines.Length < 2)
        {
            var singleRow = lines[0].Split(',', StringSplitOptions.TrimEntries);
            if (singleRow.Length < 7)
            {
                return null;
            }

            var closeRaw = singleRow[6];
            if (string.Equals(closeRaw, "N/D", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!decimal.TryParse(closeRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var directClose))
            {
                return null;
            }

            return directClose > 0m ? decimal.Round(directClose, 4, MidpointRounding.AwayFromZero) : null;
        }

        var headers = lines[0].Split(',', StringSplitOptions.TrimEntries);
        var values = lines[1].Split(',', StringSplitOptions.TrimEntries);
        var closeIndex = Array.FindIndex(headers, header => string.Equals(header, "Close", StringComparison.OrdinalIgnoreCase));

        if (closeIndex < 0 || closeIndex >= values.Length)
        {
            return null;
        }

        var raw = values[closeIndex];
        if (string.Equals(raw, "N/D", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var close))
        {
            return null;
        }

        return close > 0m ? decimal.Round(close, 4, MidpointRounding.AwayFromZero) : null;
    }

    private bool IsFresh(DateTimeOffset fetchedAtUtc)
    {
        return fetchedAtUtc >= DateTimeOffset.UtcNow.AddMinutes(-options.Value.CacheTtlMinutes);
    }

    private static string BuildCacheKey(string normalizedSymbol)
    {
        return $"market-price:{normalizedSymbol}";
    }

    private static string NormalizeSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ValidationException("Symbol is required.");
        }

        var normalized = symbol.Trim().ToUpperInvariant();
        if (normalized.Length > 20)
        {
            throw new ValidationException("Symbol is too long.");
        }

        return normalized;
    }
}
