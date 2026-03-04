using System.ComponentModel.DataAnnotations;
using Budget.Api.Infrastructure.Persistence;
using Budget.Api.Modules.Budget.Contracts;
using Budget.Api.Modules.Budget.Domain;
using Budget.Api.Modules.Budget.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Budget.Api.Tests;

public sealed class BudgetServiceValidationTests
{
    [Fact]
    public async Task UpsertAnnualPlanAsync_RejectsDuplicateCategoryMonthCells()
    {
        await using var dbContext = CreateDbContext();

        var category = new BudgetCategory
        {
            Name = "Salary",
            Section = BudgetSection.Income,
            SortOrder = 1
        };
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();

        var service = CreateAnnualPlanService(dbContext, new FakeMarketPriceService());
        var request = new UpdateAnnualPlanRequest(
            [
                new AnnualCellInput(category.Id, 1, 1000m),
                new AnnualCellInput(category.Id, 1, 2000m)
            ]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpsertAnnualPlanAsync(2026, request, "owner@example.com", CancellationToken.None));

        Assert.Contains("Duplicate annual plan cells", exception.Message);
    }

    [Fact]
    public async Task UpsertAccountSnapshotsAsync_RejectsDuplicateAccountsInRequest()
    {
        await using var dbContext = CreateDbContext();

        var account = new BudgetAccount
        {
            Name = "Main",
            Kind = BudgetAccountKind.Bank,
            Currency = "PLN",
            CurrentBalance = 1000m
        };
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        var service = CreateAccountsService(dbContext, new FakeMarketPriceService());
        var request = new UpsertMonthlyAccountSnapshotsRequest(
            [
                new AccountSnapshotInput(account.Id, 1000m, 1000m),
                new AccountSnapshotInput(account.Id, 1100m, 1100m)
            ]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpsertAccountSnapshotsAsync(2026, 3, request, "owner@example.com", CancellationToken.None));

        Assert.Contains("Duplicate account snapshots", exception.Message);
    }

    [Fact]
    public async Task GetInvestmentsAsync_RefreshModeNone_DoesNotCallPriceProvider()
    {
        await using var dbContext = CreateDbContext();

        var account = new BudgetAccount
        {
            Name = "Brokerage",
            Kind = BudgetAccountKind.Brokerage,
            Currency = "USD",
            CurrentBalance = 0m
        };
        dbContext.Accounts.Add(account);
        dbContext.InvestmentHoldings.Add(new InvestmentHolding
        {
            Account = account,
            Symbol = "AAPL",
            Units = 10m,
            AverageCost = 100m,
            LastFetchedPrice = 100m,
            LastPriceUpdatedAt = DateTimeOffset.UtcNow.AddDays(-10),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-10)
        });
        await dbContext.SaveChangesAsync();

        var fakeMarketService = new FakeMarketPriceService();
        var service = CreateInvestmentsService(dbContext, fakeMarketService);

        var response = await service.GetInvestmentsAsync("owner@example.com", "none", CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(0, fakeMarketService.GetPricesCallCount);
    }

    private static AnnualPlanService CreateAnnualPlanService(BudgetDbContext dbContext, IMarketPriceService marketPriceService)
    {
        return new AnnualPlanService(
            dbContext,
            new StubHttpClientFactory(),
            marketPriceService,
            NullLogger<AnnualPlanService>.Instance);
    }

    private static AccountsService CreateAccountsService(BudgetDbContext dbContext, IMarketPriceService marketPriceService)
    {
        return new AccountsService(
            dbContext,
            new StubHttpClientFactory(),
            marketPriceService,
            NullLogger<AccountsService>.Instance);
    }

    private static InvestmentsService CreateInvestmentsService(BudgetDbContext dbContext, IMarketPriceService marketPriceService)
    {
        return new InvestmentsService(
            dbContext,
            new StubHttpClientFactory(),
            marketPriceService,
            NullLogger<InvestmentsService>.Instance);
    }

    private static BudgetDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BudgetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new BudgetDbContext(options);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class FakeMarketPriceService : IMarketPriceService
    {
        public int CacheTtlMinutes => 30;
        public int GetPricesCallCount { get; private set; }

        public bool TryGetFreshCachedPrice(string symbol, out MarketPriceCacheSnapshot? snapshot)
        {
            snapshot = null;
            return false;
        }

        public Task<MarketPriceLookupResult> GetPriceAsync(string symbol, bool forceRefresh, CancellationToken cancellationToken)
        {
            return Task.FromResult(new MarketPriceLookupResult(symbol, null, null, FromCache: false, ProviderFailed: false));
        }

        public Task<IReadOnlyDictionary<string, MarketPriceLookupResult>> GetPricesAsync(
            IEnumerable<string> symbols,
            bool forceRefresh,
            CancellationToken cancellationToken)
        {
            GetPricesCallCount++;
            IReadOnlyDictionary<string, MarketPriceLookupResult> empty = new Dictionary<string, MarketPriceLookupResult>();
            return Task.FromResult(empty);
        }
    }
}
