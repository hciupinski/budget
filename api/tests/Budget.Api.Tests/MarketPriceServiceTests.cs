using System.Net;
using System.Net.Http;
using System.Text;
using Budget.Api.Modules.Budget.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Budget.Api.Tests;

public sealed class MarketPriceServiceTests
{
    [Fact]
    public async Task GetPriceAsync_UsesCacheInsideTtl()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(StooqCsv("123.45", "120.01"), Encoding.UTF8, "text/csv")
            }));
        using var client = new HttpClient(handler);

        var service = CreateService(cache, client);

        var first = await service.GetPriceAsync("aapl", forceRefresh: false, CancellationToken.None);
        var second = await service.GetPriceAsync("AAPL", forceRefresh: false, CancellationToken.None);

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(123.45m, first.CurrentClosePrice);
        Assert.Equal(120.01m, first.PreviousClosePrice);
        Assert.False(first.FromCache);
        Assert.Equal(123.45m, second.CurrentClosePrice);
        Assert.Equal(120.01m, second.PreviousClosePrice);
        Assert.True(second.FromCache);
    }

    [Fact]
    public async Task GetPriceAsync_RefreshesWhenCachedValueIsStale()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set(
            "market-price:AAPL",
            new MarketPriceCacheEntry(
                CurrentClosePrice: 99m,
                CurrentCloseAt: DateTimeOffset.UtcNow.AddMinutes(-31),
                FetchedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-31),
                PreviousClosePrice: 95m,
                PreviousCloseAt: DateTimeOffset.UtcNow.AddDays(-1)));

        using var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(StooqCsv("150.25", "149.10"), Encoding.UTF8, "text/csv")
            }));
        using var client = new HttpClient(handler);

        var service = CreateService(cache, client);

        var result = await service.GetPriceAsync("AAPL", forceRefresh: false, CancellationToken.None);

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(150.25m, result.CurrentClosePrice);
        Assert.False(result.FromCache);
    }

    [Fact]
    public async Task GetPriceAsync_MarksProviderFailureWhenRequestThrows()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("boom"));
        using var client = new HttpClient(handler);

        var service = CreateService(cache, client);

        var result = await service.GetPriceAsync("AAPL", forceRefresh: true, CancellationToken.None);

        Assert.Equal(1, handler.RequestCount);
        Assert.Null(result.CurrentClosePrice);
        Assert.Null(result.PreviousClosePrice);
        Assert.True(result.ProviderFailed);
        Assert.False(result.FromCache);
    }

    [Fact]
    public async Task GetPriceAsync_DeduplicatesConcurrentRequestsPerSymbol()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var handler = new StubHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(120, ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(StooqCsv("321.10", "320.00"), Encoding.UTF8, "text/csv")
            };
        });
        using var client = new HttpClient(handler);

        var service = CreateService(cache, client);

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => service.GetPriceAsync("AAPL", forceRefresh: false, CancellationToken.None));

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, handler.RequestCount);
        Assert.All(results, item =>
        {
            Assert.Equal(321.10m, item.CurrentClosePrice);
            Assert.Equal(320.00m, item.PreviousClosePrice);
        });
    }

    [Fact]
    public async Task GetPriceAsync_WhenOnlySingleDayAvailable_PreviousCloseIsNull()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(StooqCsvSingle("123.45"), Encoding.UTF8, "text/csv")
            }));
        using var client = new HttpClient(handler);

        var service = CreateService(cache, client);

        var result = await service.GetPriceAsync("AAPL", forceRefresh: true, CancellationToken.None);

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(123.45m, result.CurrentClosePrice);
        Assert.Null(result.PreviousClosePrice);
    }

    private static MarketPriceService CreateService(IMemoryCache cache, HttpClient client)
    {
        var options = Options.Create(new MarketPricesOptions
        {
            CacheTtlMinutes = 30,
            ProviderTimeoutSeconds = 5,
            MaxParallelSymbolFetches = 4
        });

        return new MarketPriceService(
            new StubHttpClientFactory(client),
            cache,
            options,
            NullLogger<MarketPriceService>.Instance);
    }

    private static string StooqCsv(string currentClose, string previousClose)
    {
        return string.Join(
            "\n",
            "Symbol,Date,Time,Open,High,Low,Close,Volume",
            $"AAPL.US,2026-03-04,17:00:00,100,100,100,{currentClose},1000",
            $"AAPL.US,2026-03-03,17:00:00,100,100,100,{previousClose},1000");
    }

    private static string StooqCsvSingle(string close)
    {
        return $"Symbol,Date,Time,Open,High,Low,Close,Volume\nAAPL.US,2026-03-04,17:00:00,100,100,100,{close},1000";
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        private int requestCount;

        public int RequestCount => requestCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref requestCount);
            return responder(request, cancellationToken);
        }
    }
}
