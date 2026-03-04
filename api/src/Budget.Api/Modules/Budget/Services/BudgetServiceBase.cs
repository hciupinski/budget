using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Budget.Api.Infrastructure.Persistence;
using Budget.Api.Modules.Budget.Contracts;
using Budget.Api.Modules.Budget.Domain;
using Microsoft.EntityFrameworkCore;

namespace Budget.Api.Modules.Budget.Services;

public abstract class BudgetServiceBase(
    BudgetDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IMarketPriceService marketPriceService,
    ILogger logger)
{
    protected readonly BudgetDbContext dbContext = dbContext;
    protected readonly IHttpClientFactory httpClientFactory = httpClientFactory;
    protected readonly IMarketPriceService marketPriceService = marketPriceService;
    protected readonly ILogger logger = logger;

    protected const string SectionsStateKey = "managed_sections";
    protected const string PlannerCustomizationStateKey = "planner_customization";
    protected const string GeneralSettingsStateKey = "general_settings";

    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    protected static readonly HashSet<string> ManagedSectionKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "INCOME",
        "BUSINESS_EXPENSES",
        "PERSONAL_EXPENSES",
        "SAVINGS",
        "INVESTMENTS"
    };

    protected static readonly HashSet<string> MonthlyStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "PLANNED",
        "DONE",
        "PARTIAL",
        "SKIPPED"
    };

    protected static readonly HashSet<string> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "PLN",
        "USD",
        "EUR"
    };

    protected static readonly HashSet<string> SupportedThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "LIGHT",
        "DARK"
    };

    protected static readonly HashSet<string> SupportedAccountKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "BANK",
        "SAVINGS",
        "BROKERAGE",
        "CASH_BUCKET"
    };

    protected static readonly IReadOnlyList<ManagedSectionStateItem> DefaultManagedSections =
    [
        new ManagedSectionStateItem
        {
            Id = "sec-income",
            Name = "Income",
            Kind = "INCOME",
            Keywords = ["income", "client", "consulting", "development"],
            Order = 0
        },
        new ManagedSectionStateItem
        {
            Id = "sec-business-costs",
            Name = "Business Expenses",
            Kind = "BUSINESS_EXPENSES",
            Keywords = ["software", "office", "professional", "marketing", "accountant", "tools"],
            Order = 1
        },
        new ManagedSectionStateItem
        {
            Id = "sec-personal-costs",
            Name = "Personal Expenses",
            Kind = "PERSONAL_EXPENSES",
            Keywords = ["housing", "food", "transportation", "healthcare", "utilities", "personal", "groceries"],
            Order = 2
        },
        new ManagedSectionStateItem
        {
            Id = "sec-savings",
            Name = "Savings",
            Kind = "SAVINGS",
            Keywords = ["savings", "emergency"],
            Order = 3
        },
        new ManagedSectionStateItem
        {
            Id = "sec-investments",
            Name = "Investments",
            Kind = "INVESTMENTS",
            Keywords = ["investment", "portfolio", "retirement", "401k", "stock"],
            Order = 4
        }
    ];
    protected async Task<T?> LoadUiStateAsync<T>(string stateKey, CancellationToken cancellationToken)
    {
        var entry = await dbContext.UiStateEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.StateKey == stateKey, cancellationToken);

        if (entry is null || string.IsNullOrWhiteSpace(entry.Value))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(entry.Value, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    protected async Task UpsertUiStateEntryAsync<T>(
        string stateKey,
        T payload,
        CancellationToken cancellationToken)
    {
        var serialized = JsonSerializer.Serialize(payload, JsonOptions);
        var now = DateTimeOffset.UtcNow;

        var entry = await dbContext.UiStateEntries
            .SingleOrDefaultAsync(x => x.StateKey == stateKey, cancellationToken);

        if (entry is null)
        {
            dbContext.UiStateEntries.Add(new BudgetUiStateEntry
            {
                StateKey = stateKey,
                Value = serialized,
                UpdatedAt = now
            });

            return;
        }

        entry.Value = serialized;
        entry.UpdatedAt = now;
    }

    protected static PlannerCustomizationResponse ToPlannerCustomizationResponse(PlannerCustomizationState state)
    {
        return new PlannerCustomizationResponse(
            state.AnnualCustomItems.Select(x => new AnnualCustomItemResponse(
                x.Id,
                x.Year,
                x.SectionId,
                x.SectionKind,
                x.Name,
                x.Months)).ToArray(),
            state.MonthlyCustomItems.Select(x => new MonthlyCustomItemResponse(
                x.Id,
                x.Year,
                x.Month,
                x.SectionId,
                x.SectionKind,
                x.Name,
                x.PlannedAmount,
                x.ActualAmount,
                x.Status,
                x.AnnualCustomItemId)).ToArray(),
            state.NameOverrides,
            state.HiddenAnnualApiRows,
            state.HiddenMonthlyApiRows,
            state.OneTimeAnnualRows);
    }

    protected static List<ManagedSectionStateItem> NormalizeManagedSections(IEnumerable<ManagedSectionStateItem>? sections)
    {
        var result = new List<ManagedSectionStateItem>();
        var source = sections?.ToList() ?? [];
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < source.Count; index++)
        {
            var input = source[index];
            var normalizedId = string.IsNullOrWhiteSpace(input.Id)
                ? $"sec-{index}"
                : input.Id.Trim();

            if (!seenIds.Add(normalizedId))
            {
                continue;
            }

            var kind = NormalizeSectionKind(input.Kind);
            var keywords = input.Keywords
                .Select(x => x?.Trim().ToLowerInvariant())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(x => x!)
                .ToList();

            result.Add(new ManagedSectionStateItem
            {
                Id = normalizedId,
                Name = string.IsNullOrWhiteSpace(input.Name) ? "Untitled Section" : input.Name.Trim(),
                Kind = kind,
                Keywords = keywords,
                Order = input.Order
            });
        }

        if (result.Count == 0)
        {
            result = DefaultManagedSections.Select(x => x.Clone()).ToList();
        }
        else
        {
            result = result
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select((item, index) =>
                {
                    item.Order = index;
                    return item;
                })
                .ToList();
        }

        return result;
    }

    protected static PlannerCustomizationState NormalizePlannerCustomization(PlannerCustomizationState? state)
    {
        state ??= new PlannerCustomizationState();

        var annual = new List<AnnualCustomItemState>();
        var annualIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < state.AnnualCustomItems.Count; index++)
        {
            var item = state.AnnualCustomItems[index];
            if (item.Year is < 2000 or > 2100)
            {
                continue;
            }

            var id = string.IsNullOrWhiteSpace(item.Id) ? $"annual-{index}" : item.Id.Trim();
            if (!annualIds.Add(id))
            {
                continue;
            }

            annual.Add(new AnnualCustomItemState
            {
                Id = id,
                Year = item.Year,
                SectionId = string.IsNullOrWhiteSpace(item.SectionId) ? "sec-personal-costs" : item.SectionId.Trim(),
                SectionKind = NormalizeSectionKind(item.SectionKind),
                Name = string.IsNullOrWhiteSpace(item.Name) ? "New Item" : item.Name.Trim(),
                Months = Enumerable.Range(0, 12)
                    .Select(monthIndex => decimal.Round(
                        item.Months.ElementAtOrDefault(monthIndex),
                        2,
                        MidpointRounding.AwayFromZero))
                    .ToList()
            });
        }

        var monthly = new List<MonthlyCustomItemState>();
        var monthlyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < state.MonthlyCustomItems.Count; index++)
        {
            var item = state.MonthlyCustomItems[index];
            if (item.Year is < 2000 or > 2100 || item.Month is < 1 or > 12)
            {
                continue;
            }

            var id = string.IsNullOrWhiteSpace(item.Id) ? $"monthly-{index}" : item.Id.Trim();
            if (!monthlyIds.Add(id))
            {
                continue;
            }

            monthly.Add(new MonthlyCustomItemState
            {
                Id = id,
                Year = item.Year,
                Month = item.Month,
                SectionId = string.IsNullOrWhiteSpace(item.SectionId) ? "sec-personal-costs" : item.SectionId.Trim(),
                SectionKind = NormalizeSectionKind(item.SectionKind),
                Name = string.IsNullOrWhiteSpace(item.Name) ? "New Item" : item.Name.Trim(),
                PlannedAmount = decimal.Round(item.PlannedAmount, 2, MidpointRounding.AwayFromZero),
                ActualAmount = item.ActualAmount.HasValue
                    ? decimal.Round(item.ActualAmount.Value, 2, MidpointRounding.AwayFromZero)
                    : null,
                Status = NormalizeMonthlyStatus(item.Status),
                AnnualCustomItemId = string.IsNullOrWhiteSpace(item.AnnualCustomItemId) ? null : item.AnnualCustomItemId.Trim()
            });
        }

        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in state.NameOverrides)
        {
            var normalizedKey = key?.Trim();
            var normalizedValue = value?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedKey) || string.IsNullOrWhiteSpace(normalizedValue))
            {
                continue;
            }

            overrides[normalizedKey] = normalizedValue;
        }

        var hiddenAnnual = state.HiddenAnnualApiRows
            .Select(x => x?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(x => x!)
            .ToList();

        var hiddenMonthly = state.HiddenMonthlyApiRows
            .Select(x => x?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(x => x!)
            .ToList();

        var oneTimeAnnualRows = state.OneTimeAnnualRows
            .Select(x => x?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(x => x!)
            .ToList();

        return new PlannerCustomizationState
        {
            AnnualCustomItems = annual,
            MonthlyCustomItems = monthly,
            NameOverrides = overrides,
            HiddenAnnualApiRows = hiddenAnnual,
            HiddenMonthlyApiRows = hiddenMonthly,
            OneTimeAnnualRows = oneTimeAnnualRows
        };
    }

    protected static BudgetAccountResponse ToAccountResponse(BudgetAccount account)
    {
        return new BudgetAccountResponse(
            account.Id,
            account.Name,
            ToWireValue(account.Kind),
            NormalizeCurrencyCode(account.Currency),
            account.CurrentBalance,
            account.IsArchived,
            account.UpdatedAt);
    }

    protected static InvestmentHoldingResponse ToHoldingResponse(
        InvestmentHolding holding,
        decimal? fetchedPriceOverride = null,
        DateTimeOffset? fetchedAtOverride = null)
    {
        var fetchedPrice = fetchedPriceOverride ?? holding.LastFetchedPrice;
        var fetchedAt = fetchedAtOverride ?? holding.LastPriceUpdatedAt;
        var effectivePrice = holding.ManualPriceOverride ?? fetchedPrice;
        var currentValue = decimal.Round(holding.Units * effectivePrice, 2, MidpointRounding.AwayFromZero);
        var costBasis = decimal.Round(holding.Units * holding.AverageCost, 2, MidpointRounding.AwayFromZero);
        var profitLoss = decimal.Round(currentValue - costBasis, 2, MidpointRounding.AwayFromZero);

        return new InvestmentHoldingResponse(
            holding.Id,
            holding.AccountId,
            holding.Account.Name,
            NormalizeCurrencyCode(holding.Account.Currency),
            holding.Symbol,
            holding.Units,
            holding.AverageCost,
            holding.ManualPriceOverride,
            fetchedPrice,
            effectivePrice,
            currentValue,
            costBasis,
            profitLoss,
            fetchedAt);
    }

    protected static InvestmentsDashboardResponse BuildInvestmentsDashboard(
        IReadOnlyList<InvestmentHoldingResponse> holdings)
    {
        var totalValue = holdings.Sum(x => x.CurrentValue);
        var totalCostBasis = holdings.Sum(x => x.CostBasis);
        var totalProfitLoss = holdings.Sum(x => x.ProfitLoss);

        var allocation = holdings
            .Where(x => x.CurrentValue > 0m)
            .OrderByDescending(x => x.CurrentValue)
            .Select(x => new InvestmentAllocationResponse(
                x.Id,
                x.Symbol,
                x.CurrentValue,
                totalValue <= 0m
                    ? 0m
                    : decimal.Round((x.CurrentValue / totalValue) * 100m, 2, MidpointRounding.AwayFromZero)))
            .ToArray();

        return new InvestmentsDashboardResponse(
            TotalValue: totalValue,
            TotalCostBasis: totalCostBasis,
            TotalProfitLoss: totalProfitLoss,
            Allocation: allocation);
    }

    protected async Task<AssetsOverviewSummaryResponse> BuildAssetsSummaryAsync(
        string baseCurrency,
        IReadOnlyList<BudgetAccount> accounts,
        IReadOnlyList<AccountSnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        var rates = await ResolveExchangeRatesAsync(
            baseCurrency,
            accounts.Select(x => x.Currency).Distinct(StringComparer.OrdinalIgnoreCase),
            cancellationToken);
        var ratesWithBase = new Dictionary<string, decimal>(rates, StringComparer.OrdinalIgnoreCase)
        {
            [baseCurrency] = 1m
        };

        var netWorth = decimal.Round(accounts
            .Where(x => !x.IsArchived)
            .Sum(x => x.CurrentBalance * ratesWithBase[NormalizeCurrencyCode(x.Currency)]), 2, MidpointRounding.AwayFromZero);
        var snapshotPlanned = decimal.Round(snapshots
            .Sum(x => x.PlannedBalance * ratesWithBase[NormalizeCurrencyCode(x.Account.Currency)]), 2, MidpointRounding.AwayFromZero);
        var snapshotActual = decimal.Round(snapshots
            .Sum(x => (x.ActualBalance ?? x.PlannedBalance) * ratesWithBase[NormalizeCurrencyCode(x.Account.Currency)]), 2, MidpointRounding.AwayFromZero);

        return new AssetsOverviewSummaryResponse(
            BaseCurrency: baseCurrency,
            NetWorth: netWorth,
            SnapshotPlanned: snapshotPlanned,
            SnapshotActual: snapshotActual,
            ExchangeRates: ratesWithBase);
    }

    protected async Task<Dictionary<string, decimal>> ResolveExchangeRatesAsync(
        string baseCurrency,
        IEnumerable<string> currencies,
        CancellationToken cancellationToken)
    {
        var normalizedBase = NormalizeCurrencyCode(baseCurrency);
        var distinct = currencies
            .Select(NormalizeCurrencyCode)
            .Where(x => !string.Equals(x, normalizedBase, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (distinct.Length == 0)
        {
            return result;
        }

        foreach (var currency in distinct)
        {
            var rate = await FetchExchangeRateAsync(currency, normalizedBase, cancellationToken);
            result[currency] = rate;
        }

        return result;
    }

    protected async Task<decimal> FetchExchangeRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        try
        {
            var client = httpClientFactory.CreateClient("fx-rates");
            client.Timeout = TimeSpan.FromSeconds(5);
            var url = $"https://api.frankfurter.app/latest?from={fromCurrency}&to={toCurrency}";
            var payload = await client.GetFromJsonAsync<FrankfurterResponse>(url, cancellationToken);
            if (payload?.Rates is null)
            {
                return 1m;
            }

            if (!payload.Rates.TryGetValue(toCurrency, out var rate))
            {
                return 1m;
            }

            if (rate <= 0m)
            {
                return 1m;
            }

            return decimal.Round(rate, 6, MidpointRounding.AwayFromZero);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to fetch FX rate {From}->{To}. Falling back to 1.", fromCurrency, toCurrency);
            return 1m;
        }
    }

    protected static SavingsGoalResponse ToSavingsGoalResponse(
        SavingsGoal goal,
        IReadOnlyDictionary<Guid, BudgetAccount> accountsById)
    {
        decimal currentAmount;
        string? accountName = null;

        if (goal.AccountId.HasValue && accountsById.TryGetValue(goal.AccountId.Value, out var account))
        {
            currentAmount = account.CurrentBalance;
            accountName = account.Name;
        }
        else
        {
            currentAmount = goal.CurrentAmount;
        }

        var progressPercent = goal.TargetAmount <= 0m
            ? 0m
            : decimal.Round((currentAmount / goal.TargetAmount) * 100m, 2, MidpointRounding.AwayFromZero);

        return new SavingsGoalResponse(
            goal.Id,
            goal.Name,
            goal.AccountId,
            accountName,
            goal.TargetAmount,
            currentAmount,
            goal.MonthlyContributionTarget,
            goal.TargetYear,
            goal.TargetMonth,
            progressPercent,
            goal.UpdatedAt);
    }

    protected static string NormalizeSectionKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return "PERSONAL_EXPENSES";
        }

        var normalized = kind.Trim().ToUpperInvariant();
        return ManagedSectionKinds.Contains(normalized) ? normalized : "PERSONAL_EXPENSES";
    }

    protected static BudgetAccountKind NormalizeAccountKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ValidationException("Account kind is required.");
        }

        var normalized = kind.Trim().ToUpperInvariant();
        if (!SupportedAccountKinds.Contains(normalized))
        {
            throw new ValidationException($"Unsupported account kind '{kind}'.");
        }

        return normalized switch
        {
            "BANK" => BudgetAccountKind.Bank,
            "SAVINGS" => BudgetAccountKind.Savings,
            "BROKERAGE" => BudgetAccountKind.Brokerage,
            "CASH_BUCKET" => BudgetAccountKind.CashBucket,
            _ => throw new ValidationException($"Unsupported account kind '{kind}'.")
        };
    }

    protected static string NormalizeMonthlyStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "PLANNED";
        }

        var normalized = status.Trim().ToUpperInvariant();
        return MonthlyStatuses.Contains(normalized) ? normalized : "PLANNED";
    }

    protected static string NormalizeCurrencyCode(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return "PLN";
        }

        var normalized = currency.Trim().ToUpperInvariant();
        return SupportedCurrencies.Contains(normalized) ? normalized : "PLN";
    }

    protected static string NormalizeThemeMode(string? theme)
    {
        if (string.IsNullOrWhiteSpace(theme))
        {
            return "LIGHT";
        }

        var normalized = theme.Trim().ToUpperInvariant();
        return SupportedThemes.Contains(normalized) ? normalized : "LIGHT";
    }

    protected static string NormalizeSymbol(string symbol)
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

    protected static void ValidateSavingsGoalDate(int? targetYear, int? targetMonth)
    {
        if (!targetYear.HasValue && !targetMonth.HasValue)
        {
            return;
        }

        if (!targetYear.HasValue || !targetMonth.HasValue)
        {
            throw new ValidationException("Savings goal target date requires both year and month.");
        }

        ValidateYear(targetYear.Value);
        ValidateMonth(targetMonth.Value);
    }

    protected sealed class FrankfurterResponse
    {
        public Dictionary<string, decimal> Rates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    protected static void ValidateYear(int year)
    {
        if (year < 2000 || year > 2100)
        {
            throw new ValidationException($"Unsupported year '{year}'.");
        }
    }

    protected static void ValidateMonth(int month)
    {
        if (month < 1 || month > 12)
        {
            throw new ValidationException($"Unsupported month '{month}'.");
        }
    }

    protected static MonthlyActionStatus? NormalizeStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status) || string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!Enum.TryParse<MonthlyActionStatus>(status, true, out var parsed))
        {
            throw new ValidationException($"Unsupported status filter '{status}'.");
        }

        return parsed;
    }

    protected static RefreshMode ParseRefreshMode(string? refreshMode)
    {
        if (string.IsNullOrWhiteSpace(refreshMode) ||
            string.Equals(refreshMode, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return RefreshMode.Auto;
        }

        if (string.Equals(refreshMode, "none", StringComparison.OrdinalIgnoreCase))
        {
            return RefreshMode.None;
        }

        throw new ValidationException($"Unsupported refreshMode '{refreshMode}'. Allowed values: auto, none.");
    }

    protected static AnnualSummaryResponse BuildAnnualSummary(IEnumerable<AnnualCategoryRowResponse> rows)
    {
        var income = rows.Where(x => x.Section == ToWireValue(BudgetSection.Income)).Sum(x => x.Total);
        var costs = rows.Where(x => x.Section == ToWireValue(BudgetSection.Costs)).Sum(x => x.Total);
        var savings = rows.Where(x => x.Section == ToWireValue(BudgetSection.SavingsInvestments)).Sum(x => x.Total);
        var remainder = income - costs - savings;

        return new AnnualSummaryResponse(
            Income: income,
            Costs: costs,
            SavingsInvestments: savings,
            Remainder: remainder,
            GrandTotal: income + costs + savings);
    }

    protected static MonthlySummaryCardsResponse BuildMonthlySummary(IReadOnlyCollection<MonthlyAction> actions)
    {
        var incomePlanned = actions.Where(x => x.Category.Section == BudgetSection.Income).Sum(x => x.PlannedAmount);
        var costsPlanned = actions.Where(x => x.Category.Section == BudgetSection.Costs).Sum(x => x.PlannedAmount);
        var savingsPlanned = actions.Where(x => x.Category.Section == BudgetSection.SavingsInvestments).Sum(x => x.PlannedAmount);

        var incomeActual = actions.Where(x => x.Category.Section == BudgetSection.Income).Sum(ResolveActualAmount);
        var costsActual = actions.Where(x => x.Category.Section == BudgetSection.Costs).Sum(ResolveActualAmount);
        var savingsActual = actions.Where(x => x.Category.Section == BudgetSection.SavingsInvestments).Sum(ResolveActualAmount);

        return new MonthlySummaryCardsResponse(
            IncomePlanned: incomePlanned,
            IncomeActual: incomeActual,
            CostsPlanned: costsPlanned,
            CostsActual: costsActual,
            SavingsPlanned: savingsPlanned,
            SavingsActual: savingsActual,
            RemainderPlanned: incomePlanned - costsPlanned - savingsPlanned,
            RemainderActual: incomeActual - costsActual - savingsActual);
    }

    protected static decimal ResolveActualAmount(MonthlyAction action)
    {
        return action.Status switch
        {
            MonthlyActionStatus.Skipped => 0m,
            MonthlyActionStatus.Done => action.ActualAmount ?? action.PlannedAmount,
            MonthlyActionStatus.Partial => action.ActualAmount ?? 0m,
            _ => 0m
        };
    }

    public static string ToWireValue(BudgetSection section)
    {
        return section switch
        {
            BudgetSection.Income => "INCOME",
            BudgetSection.Costs => "COSTS",
            BudgetSection.SavingsInvestments => "SAVINGS_INVESTMENTS",
            _ => section.ToString().ToUpper(CultureInfo.InvariantCulture)
        };
    }

    public static string ToWireValue(MonthlyActionStatus status)
    {
        return status switch
        {
            MonthlyActionStatus.Planned => "PLANNED",
            MonthlyActionStatus.Done => "DONE",
            MonthlyActionStatus.Partial => "PARTIAL",
            MonthlyActionStatus.Skipped => "SKIPPED",
            _ => status.ToString().ToUpper(CultureInfo.InvariantCulture)
        };
    }

    public static string ToWireValue(BudgetAccountKind kind)
    {
        return kind switch
        {
            BudgetAccountKind.Bank => "BANK",
            BudgetAccountKind.Savings => "SAVINGS",
            BudgetAccountKind.Brokerage => "BROKERAGE",
            BudgetAccountKind.CashBucket => "CASH_BUCKET",
            _ => kind.ToString().ToUpper(CultureInfo.InvariantCulture)
        };
    }

    protected enum RefreshMode
    {
        Auto = 1,
        None = 2
    }

    protected sealed class ManagedSectionsState
    {
        public List<ManagedSectionStateItem> Sections { get; set; } = [];
    }

    protected sealed class ManagedSectionStateItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = "PERSONAL_EXPENSES";
        public List<string> Keywords { get; set; } = [];
        public int Order { get; set; }

        public ManagedSectionStateItem Clone()
        {
            return new ManagedSectionStateItem
            {
                Id = Id,
                Name = Name,
                Kind = Kind,
                Keywords = [.. Keywords],
                Order = Order
            };
        }
    }

    protected sealed class PlannerCustomizationState
    {
        public List<AnnualCustomItemState> AnnualCustomItems { get; set; } = [];
        public List<MonthlyCustomItemState> MonthlyCustomItems { get; set; } = [];
        public Dictionary<string, string> NameOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> HiddenAnnualApiRows { get; set; } = [];
        public List<string> HiddenMonthlyApiRows { get; set; } = [];
        public List<string> OneTimeAnnualRows { get; set; } = [];
    }

    protected sealed class AnnualCustomItemState
    {
        public string Id { get; set; } = string.Empty;
        public int Year { get; set; }
        public string SectionId { get; set; } = string.Empty;
        public string SectionKind { get; set; } = "PERSONAL_EXPENSES";
        public string Name { get; set; } = string.Empty;
        public List<decimal> Months { get; set; } = [];
    }

    protected sealed class MonthlyCustomItemState
    {
        public string Id { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
        public string SectionId { get; set; } = string.Empty;
        public string SectionKind { get; set; } = "PERSONAL_EXPENSES";
        public string Name { get; set; } = string.Empty;
        public decimal PlannedAmount { get; set; }
        public decimal? ActualAmount { get; set; }
        public string Status { get; set; } = "PLANNED";
        public string? AnnualCustomItemId { get; set; }
    }

    protected sealed class GeneralSettingsState
    {
        public string Currency { get; set; } = "PLN";
        public string Theme { get; set; } = "LIGHT";
    }
}
