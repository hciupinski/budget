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
                snapshot = new MarketPriceCacheSnapshot(
                    cached.CurrentClosePrice,
                    cached.CurrentCloseAt,
                    cached.PreviousClosePrice,
                    cached.PreviousCloseAt);
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
            return new MarketPriceLookupResult(
                normalized,
                cached.CurrentClosePrice,
                cached.CurrentCloseAt,
                cached.PreviousClosePrice,
                cached.PreviousCloseAt,
                FromCache: true,
                ProviderFailed: false);
        }

        var symbolLock = symbolLocks.GetOrAdd(normalized, _ => new SemaphoreSlim(1, 1));
        await symbolLock.WaitAsync(cancellationToken);

        try
        {
            if (!forceRefresh && TryGetFreshCachedPrice(normalized, out cached) && cached is not null)
            {
                return new MarketPriceLookupResult(
                    normalized,
                    cached.CurrentClosePrice,
                    cached.CurrentCloseAt,
                    cached.PreviousClosePrice,
                    cached.PreviousCloseAt,
                    FromCache: true,
                    ProviderFailed: false);
            }
            var fetchResult = await FetchMarketPriceFromProviderAsync(normalized, cancellationToken);

            if (fetchResult.CurrentClosePrice.HasValue)
            {
                var currentCloseAt = fetchResult.CurrentCloseAt ?? DateTimeOffset.UtcNow;
                var entry = new MarketPriceCacheEntry(
                    fetchResult.CurrentClosePrice.Value,
                    currentCloseAt,
                    DateTimeOffset.UtcNow,
                    fetchResult.PreviousClosePrice,
                    fetchResult.PreviousCloseAt);
                memoryCache.Set(
                    BuildCacheKey(normalized),
                    entry,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(options.Value.CacheTtlMinutes)
                    });

                return new MarketPriceLookupResult(
                    normalized,
                    fetchResult.CurrentClosePrice.Value,
                    currentCloseAt,
                    fetchResult.PreviousClosePrice,
                    fetchResult.PreviousCloseAt,
                    FromCache: false,
                    ProviderFailed: false);
            }

            return new MarketPriceLookupResult(
                normalized,
                null,
                null,
                null,
                null,
                FromCache: false,
                ProviderFailed: fetchResult.ProviderFailed);
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

    private async Task<(
        decimal? CurrentClosePrice,
        DateTimeOffset? CurrentCloseAt,
        decimal? PreviousClosePrice,
        DateTimeOffset? PreviousCloseAt,
        bool ProviderFailed)> FetchMarketPriceFromProviderAsync(string normalizedSymbol, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("market-prices");
            var endpoint = $"https://stooq.com/q/d/l/?s={normalizedSymbol.ToLowerInvariant()}&i=d";
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.Value.ProviderTimeoutSeconds));
            var csv = await client.GetStringAsync(endpoint, timeoutCts.Token);
            var parsed = ParseStooqCloses(csv);
            return (
                parsed.CurrentClosePrice,
                parsed.CurrentCloseAt,
                parsed.PreviousClosePrice,
                parsed.PreviousCloseAt,
                ProviderFailed: false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to fetch market price for symbol {Symbol}.", normalizedSymbol);
            return (null, null, null, null, ProviderFailed: true);
        }
    }

    private static (
        decimal? CurrentClosePrice,
        DateTimeOffset? CurrentCloseAt,
        decimal? PreviousClosePrice,
        DateTimeOffset? PreviousCloseAt) ParseStooqCloses(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return (null, null, null, null);
        }

        var lines = csv
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length == 0)
        {
            return (null, null, null, null);
        }

        var headers = lines[0].Split(',', StringSplitOptions.TrimEntries);
        var headerHasClose = headers.Any(header => string.Equals(header, "Close", StringComparison.OrdinalIgnoreCase));
        var startIndex = headerHasClose ? 1 : 0;

        var closeIndex = Array.FindIndex(headers, header => string.Equals(header, "Close", StringComparison.OrdinalIgnoreCase));
        if (closeIndex < 0)
        {
            closeIndex = 6;
        }

        var dateIndex = Array.FindIndex(headers, header => string.Equals(header, "Date", StringComparison.OrdinalIgnoreCase));
        if (dateIndex < 0)
        {
            dateIndex = 1;
        }

        var parsedRows = new List<(decimal Close, DateTimeOffset? Date, int Order)>();
        var order = 0;

        for (var index = startIndex; index < lines.Length; index += 1)
        {
            var values = lines[index].Split(',', StringSplitOptions.TrimEntries);
            if (closeIndex < 0 || closeIndex >= values.Length)
            {
                continue;
            }

            var closeRaw = values[closeIndex];
            if (string.Equals(closeRaw, "N/D", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!decimal.TryParse(closeRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var closeValue))
            {
                continue;
            }

            if (closeValue <= 0m)
            {
                continue;
            }

            DateTimeOffset? rowDate = null;
            if (dateIndex >= 0 &&
                dateIndex < values.Length &&
                DateOnly.TryParseExact(values[dateIndex], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                rowDate = new DateTimeOffset(parsedDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            }

            parsedRows.Add((decimal.Round(closeValue, 4, MidpointRounding.AwayFromZero), rowDate, order));
            order += 1;
        }

        if (parsedRows.Count == 0)
        {
            return (null, null, null, null);
        }

        var hasAnyDate = parsedRows.Any(row => row.Date.HasValue);
        var orderedRows = hasAnyDate
            ? parsedRows
                .OrderByDescending(row => row.Date ?? DateTimeOffset.MinValue)
                .ThenBy(row => row.Order)
                .ToArray()
            : parsedRows.ToArray();

        var current = orderedRows[0];
        var previous = orderedRows.Length > 1 ? orderedRows[1] : default;

        return (
            current.Close,
            current.Date,
            orderedRows.Length > 1 ? previous.Close : null,
            orderedRows.Length > 1 ? previous.Date : null);
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
