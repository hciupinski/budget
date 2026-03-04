using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Budget.Api.Infrastructure.Persistence;
using Budget.Api.Modules.Budget.Contracts;
using Budget.Api.Modules.Budget.Domain;
using Microsoft.EntityFrameworkCore;

namespace Budget.Api.Modules.Budget.Services;

public sealed class InvestmentsService(
    BudgetDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IMarketPriceService marketPriceService,
    ILogger<InvestmentsService> logger)
    : BudgetServiceBase(dbContext, httpClientFactory, marketPriceService, logger),
      IInvestmentsService
{
    public async Task<AssetsInvestmentsResponse> GetInvestmentsAsync(
        string actor,
        string? refreshMode,
        CancellationToken cancellationToken)
    {
        var mode = ParseRefreshMode(refreshMode);
        var holdings = await dbContext.InvestmentHoldings
            .Include(x => x.Account)
            .OrderBy(x => x.Symbol)
            .ToListAsync(cancellationToken);

        if (holdings.Count == 0)
        {
            return new AssetsInvestmentsResponse(
                Holdings: [],
                Investments: BuildInvestmentsDashboard([]),
                PriceRefreshMeta: new InvestmentPriceRefreshMetaResponse(
                    RefreshedAtUtc: DateTimeOffset.UtcNow,
                    CacheTtlMinutes: marketPriceService.CacheTtlMinutes,
                    SymbolsRequested: [],
                    SymbolsRefreshed: [],
                    SymbolsFromCache: [],
                    SymbolsFallbackToStale: [],
                    HadProviderFailures: false));
        }

        var ttlMinutes = marketPriceService.CacheTtlMinutes;
        var staleThreshold = DateTimeOffset.UtcNow.AddMinutes(-ttlMinutes);

        var holdingsBySymbol = holdings
            .GroupBy(x => NormalizeSymbol(x.Symbol), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.OrdinalIgnoreCase);

        var symbolsRequested = holdingsBySymbol.Keys
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var symbolsToRefresh = new List<string>();
        var symbolsFromCache = new List<string>();
        var symbolsRefreshed = new List<string>();
        var symbolsFallbackToStale = new List<string>();
        var cachedBySymbol = new Dictionary<string, MarketPriceCacheSnapshot>(StringComparer.OrdinalIgnoreCase);
        var refreshedBySymbol = new Dictionary<string, (decimal Price, DateTimeOffset FetchedAtUtc)>(StringComparer.OrdinalIgnoreCase);
        var hadProviderFailures = false;

        foreach (var symbol in symbolsRequested)
        {
            var symbolHoldings = holdingsBySymbol[symbol];
            var hasStaleInDb = symbolHoldings.Any(x => x.LastPriceUpdatedAt < staleThreshold);
            var hasFreshCache = marketPriceService.TryGetFreshCachedPrice(symbol, out var cacheSnapshot);

            if (hasFreshCache && cacheSnapshot is not null && !hasStaleInDb)
            {
                symbolsFromCache.Add(symbol);
                cachedBySymbol[symbol] = cacheSnapshot;
                continue;
            }

            symbolsToRefresh.Add(symbol);
        }

        if (mode == RefreshMode.Auto && symbolsToRefresh.Count > 0)
        {
            var fetchedBySymbol = await marketPriceService.GetPricesAsync(symbolsToRefresh, forceRefresh: false, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var updated = 0;

            foreach (var symbol in symbolsToRefresh)
            {
                if (!fetchedBySymbol.TryGetValue(symbol, out var result))
                {
                    symbolsFallbackToStale.Add(symbol);
                    continue;
                }

                hadProviderFailures |= result.ProviderFailed;

                if (!result.Price.HasValue)
                {
                    symbolsFallbackToStale.Add(symbol);
                    continue;
                }

                var fetchedAt = result.FetchedAtUtc ?? now;
                refreshedBySymbol[symbol] = (result.Price.Value, fetchedAt);
                symbolsRefreshed.Add(symbol);

                foreach (var holding in holdingsBySymbol[symbol])
                {
                    var hasChanged =
                        holding.LastFetchedPrice != result.Price.Value ||
                        holding.LastPriceUpdatedAt < fetchedAt;

                    if (!hasChanged)
                    {
                        continue;
                    }

                    holding.LastFetchedPrice = result.Price.Value;
                    holding.LastPriceUpdatedAt = fetchedAt;
                    holding.UpdatedAt = now;
                    updated++;
                }
            }

            if (updated > 0)
            {
                dbContext.AuditEntries.Add(new AuditEntry
                {
                    EntityType = "InvestmentHolding",
                    EntityId = Guid.NewGuid(),
                    EventType = "PRICES_AUTO_REFRESHED",
                    ChangedBy = actor,
                    ChangedAt = now,
                    Payload = JsonSerializer.Serialize(new
                    {
                        updated,
                        symbolsRefreshed = symbolsRefreshed.Count,
                        symbolsFallbackToStale = symbolsFallbackToStale.Count
                    })
                });

                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        var holdingResponses = holdings.Select(holding =>
        {
            var symbol = NormalizeSymbol(holding.Symbol);

            if (refreshedBySymbol.TryGetValue(symbol, out var refreshed))
            {
                return ToHoldingResponse(holding, refreshed.Price, refreshed.FetchedAtUtc);
            }

            if (cachedBySymbol.TryGetValue(symbol, out var cached))
            {
                return ToHoldingResponse(holding, cached.Price, cached.FetchedAtUtc);
            }

            return ToHoldingResponse(holding);
        }).ToArray();

        return new AssetsInvestmentsResponse(
            Holdings: holdingResponses,
            Investments: BuildInvestmentsDashboard(holdingResponses),
            PriceRefreshMeta: new InvestmentPriceRefreshMetaResponse(
                RefreshedAtUtc: DateTimeOffset.UtcNow,
                CacheTtlMinutes: ttlMinutes,
                SymbolsRequested: symbolsRequested,
                SymbolsRefreshed: symbolsRefreshed.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
                SymbolsFromCache: symbolsFromCache.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
                SymbolsFallbackToStale: symbolsFallbackToStale.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
                HadProviderFailures: hadProviderFailures));
    }
    public async Task<InvestmentHoldingResponse> CreateHoldingAsync(
        CreateInvestmentHoldingRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == request.AccountId, cancellationToken);
        if (account is null)
        {
            throw new KeyNotFoundException("Account was not found.");
        }

        if (account.Kind != BudgetAccountKind.Brokerage)
        {
            throw new ValidationException("Holdings can be added only to BROKERAGE accounts.");
        }

        var symbol = NormalizeSymbol(request.Symbol);
        if (request.Units <= 0)
        {
            throw new ValidationException("Units must be greater than 0.");
        }

        if (request.AverageCost < 0)
        {
            throw new ValidationException("Average cost cannot be negative.");
        }

        var now = DateTimeOffset.UtcNow;
        var fetched = await marketPriceService.GetPriceAsync(symbol, forceRefresh: false, cancellationToken);
        var fetchedPrice = fetched.Price;
        var effectiveFetched = fetchedPrice ?? decimal.Round(request.AverageCost, 4, MidpointRounding.AwayFromZero);
        var holding = new InvestmentHolding
        {
            AccountId = account.Id,
            Symbol = symbol,
            Units = decimal.Round(request.Units, 6, MidpointRounding.AwayFromZero),
            AverageCost = decimal.Round(request.AverageCost, 4, MidpointRounding.AwayFromZero),
            ManualPriceOverride = request.ManualPriceOverride.HasValue
                ? decimal.Round(request.ManualPriceOverride.Value, 4, MidpointRounding.AwayFromZero)
                : null,
            LastFetchedPrice = effectiveFetched,
            LastPriceUpdatedAt = now,
            UpdatedAt = now
        };

        dbContext.InvestmentHoldings.Add(holding);
        dbContext.AuditEntries.Add(new AuditEntry
        {
            EntityType = "InvestmentHolding",
            EntityId = holding.Id,
            EventType = "HOLDING_CREATED",
            ChangedBy = actor,
            ChangedAt = now,
            Payload = JsonSerializer.Serialize(new
            {
                holding.AccountId,
                holding.Symbol,
                holding.Units,
                holding.AverageCost,
                holding.ManualPriceOverride
            })
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        holding.Account = account;

        return ToHoldingResponse(holding);
    }

    public async Task<InvestmentHoldingResponse> UpdateHoldingAsync(
        Guid holdingId,
        UpdateInvestmentHoldingRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var holding = await dbContext.InvestmentHoldings
            .Include(x => x.Account)
            .SingleOrDefaultAsync(x => x.Id == holdingId, cancellationToken);

        if (holding is null)
        {
            throw new KeyNotFoundException("Holding was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        var changed = false;

        if (request.Units.HasValue)
        {
            if (request.Units.Value <= 0)
            {
                throw new ValidationException("Units must be greater than 0.");
            }

            holding.Units = decimal.Round(request.Units.Value, 6, MidpointRounding.AwayFromZero);
            changed = true;
        }

        if (request.AverageCost.HasValue)
        {
            if (request.AverageCost.Value < 0)
            {
                throw new ValidationException("Average cost cannot be negative.");
            }

            holding.AverageCost = decimal.Round(request.AverageCost.Value, 4, MidpointRounding.AwayFromZero);
            changed = true;
        }

        if (request.ClearManualPriceOverride)
        {
            holding.ManualPriceOverride = null;
            changed = true;
        }
        else if (request.ManualPriceOverride.HasValue)
        {
            holding.ManualPriceOverride = decimal.Round(request.ManualPriceOverride.Value, 4, MidpointRounding.AwayFromZero);
            changed = true;
        }

        if (changed)
        {
            holding.UpdatedAt = now;
            dbContext.AuditEntries.Add(new AuditEntry
            {
                EntityType = "InvestmentHolding",
                EntityId = holding.Id,
                EventType = "HOLDING_UPDATED",
                ChangedBy = actor,
                ChangedAt = now,
                Payload = JsonSerializer.Serialize(new
                {
                    holding.Symbol,
                    holding.Units,
                    holding.AverageCost,
                    holding.ManualPriceOverride
                })
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ToHoldingResponse(holding);
    }

    public async Task DeleteHoldingAsync(
        Guid holdingId,
        string actor,
        CancellationToken cancellationToken)
    {
        var holding = await dbContext.InvestmentHoldings
            .SingleOrDefaultAsync(x => x.Id == holdingId, cancellationToken);

        if (holding is null)
        {
            throw new KeyNotFoundException("Holding was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        dbContext.InvestmentHoldings.Remove(holding);
        dbContext.AuditEntries.Add(new AuditEntry
        {
            EntityType = "InvestmentHolding",
            EntityId = holding.Id,
            EventType = "HOLDING_DELETED",
            ChangedBy = actor,
            ChangedAt = now,
            Payload = JsonSerializer.Serialize(new
            {
                holding.Symbol,
                holding.AccountId
            })
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RefreshInvestmentPricesResponse> RefreshInvestmentPricesAsync(
        string actor,
        CancellationToken cancellationToken)
    {
        var holdings = await dbContext.InvestmentHoldings.ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var updated = 0;
        var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalizedSymbols = holdings
            .Select(x => NormalizeSymbol(x.Symbol))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var pricesBySymbol = await marketPriceService.GetPricesAsync(normalizedSymbols, forceRefresh: true, cancellationToken);

        foreach (var holding in holdings)
        {
            var normalizedSymbol = NormalizeSymbol(holding.Symbol);
            symbols.Add(normalizedSymbol);

            if (!pricesBySymbol.TryGetValue(normalizedSymbol, out var fetchedPrice) || !fetchedPrice.Price.HasValue)
            {
                continue;
            }

            var nextPrice = fetchedPrice.Price.Value;
            var nextUpdatedAt = fetchedPrice.FetchedAtUtc ?? now;
            var hasChanged = holding.LastFetchedPrice != nextPrice || holding.LastPriceUpdatedAt < nextUpdatedAt;

            if (!hasChanged)
            {
                continue;
            }

            holding.LastFetchedPrice = nextPrice;
            holding.LastPriceUpdatedAt = nextUpdatedAt;
            holding.UpdatedAt = now;
            updated++;
        }

        if (updated > 0)
        {
            dbContext.AuditEntries.Add(new AuditEntry
            {
                EntityType = "InvestmentHolding",
                EntityId = Guid.NewGuid(),
                EventType = "PRICES_REFRESHED",
                ChangedBy = actor,
                ChangedAt = now,
                Payload = JsonSerializer.Serialize(new
                {
                    updated
                })
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new RefreshInvestmentPricesResponse(
            UpdatedCount: updated,
            RefreshedAt: now,
            Symbols: symbols.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
