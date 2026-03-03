using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Budget.Api.Infrastructure.Persistence;
using Budget.Api.Modules.Budget.Contracts;
using Budget.Api.Modules.Budget.Domain;
using Microsoft.EntityFrameworkCore;

namespace Budget.Api.Modules.Budget.Services;

public sealed class BudgetService(
    BudgetDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    ILogger<BudgetService> logger)
{
    private const string SectionsStateKey = "managed_sections";
    private const string PlannerCustomizationStateKey = "planner_customization";
    private const string GeneralSettingsStateKey = "general_settings";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> ManagedSectionKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "INCOME",
        "BUSINESS_EXPENSES",
        "PERSONAL_EXPENSES",
        "SAVINGS",
        "INVESTMENTS"
    };

    private static readonly HashSet<string> MonthlyStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "PLANNED",
        "DONE",
        "PARTIAL",
        "SKIPPED"
    };

    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "PLN",
        "USD",
        "EUR"
    };

    private static readonly HashSet<string> SupportedThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "LIGHT",
        "DARK"
    };

    private static readonly HashSet<string> SupportedAccountKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "BANK",
        "SAVINGS",
        "BROKERAGE",
        "CASH_BUCKET"
    };

    private static readonly IReadOnlyList<ManagedSectionStateItem> DefaultManagedSections =
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

    public async Task<IReadOnlyList<BudgetCategoryResponse>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        var categories = await dbContext.Categories
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .Select(x => new BudgetCategoryResponse(
                x.Id,
                x.Name,
                ToWireValue(x.Section),
                x.SortOrder))
            .ToListAsync(cancellationToken);

        return categories;
    }

    public async Task<ManagedSectionsResponse> GetManagedSectionsAsync(CancellationToken cancellationToken)
    {
        var state = await LoadUiStateAsync<ManagedSectionsState>(SectionsStateKey, cancellationToken);
        var normalized = NormalizeManagedSections(state?.Sections);

        return new ManagedSectionsResponse(
            normalized.Select(x => new ManagedSectionResponse(
                x.Id,
                x.Name,
                x.Kind,
                x.Keywords,
                x.Order)).ToArray());
    }

    public async Task<ManagedSectionsResponse> SaveManagedSectionsAsync(
        UpdateManagedSectionsRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeManagedSections(
            request.Sections.Select(x => new ManagedSectionStateItem
            {
                Id = x.Id,
                Name = x.Name,
                Kind = x.Kind,
                Keywords = x.Keywords?.ToList() ?? [],
                Order = x.Order
            }));

        await UpsertUiStateEntryAsync(
            SectionsStateKey,
            new ManagedSectionsState
            {
                Sections = normalized
            },
            cancellationToken);

        dbContext.AuditEntries.Add(new AuditEntry
        {
            EntityType = "UiSettings",
            EntityId = Guid.NewGuid(),
            EventType = "SECTIONS_UPDATED",
            ChangedBy = actor,
            ChangedAt = DateTimeOffset.UtcNow,
            Payload = JsonSerializer.Serialize(new
            {
                sections = normalized.Count
            })
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ManagedSectionsResponse(
            normalized.Select(x => new ManagedSectionResponse(
                x.Id,
                x.Name,
                x.Kind,
                x.Keywords,
                x.Order)).ToArray());
    }

    public async Task<PlannerCustomizationResponse> GetPlannerCustomizationAsync(CancellationToken cancellationToken)
    {
        var state = await LoadUiStateAsync<PlannerCustomizationState>(PlannerCustomizationStateKey, cancellationToken);
        var normalized = NormalizePlannerCustomization(state);

        return ToPlannerCustomizationResponse(normalized);
    }

    public async Task<PlannerCustomizationResponse> SavePlannerCustomizationAsync(
        UpdatePlannerCustomizationRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizePlannerCustomization(new PlannerCustomizationState
        {
            AnnualCustomItems = request.AnnualCustomItems?.Select(x => new AnnualCustomItemState
            {
                Id = x.Id,
                Year = x.Year,
                SectionId = x.SectionId,
                SectionKind = x.SectionKind,
                Name = x.Name,
                Months = x.Months?.ToList() ?? []
            }).ToList() ?? [],
            MonthlyCustomItems = request.MonthlyCustomItems?.Select(x => new MonthlyCustomItemState
            {
                Id = x.Id,
                Year = x.Year,
                Month = x.Month,
                SectionId = x.SectionId,
                SectionKind = x.SectionKind,
                Name = x.Name,
                PlannedAmount = x.PlannedAmount,
                ActualAmount = x.ActualAmount,
                Status = x.Status,
                AnnualCustomItemId = x.AnnualCustomItemId
            }).ToList() ?? [],
            NameOverrides = request.NameOverrides?.ToDictionary(x => x.Key, x => x.Value) ?? [],
            HiddenAnnualApiRows = request.HiddenAnnualApiRows?.ToList() ?? [],
            HiddenMonthlyApiRows = request.HiddenMonthlyApiRows?.ToList() ?? [],
            OneTimeAnnualRows = request.OneTimeAnnualRows?.ToList() ?? []
        });

        await UpsertUiStateEntryAsync(PlannerCustomizationStateKey, normalized, cancellationToken);

        dbContext.AuditEntries.Add(new AuditEntry
        {
            EntityType = "UiSettings",
            EntityId = Guid.NewGuid(),
            EventType = "PLANNER_CUSTOMIZATION_UPDATED",
            ChangedBy = actor,
            ChangedAt = DateTimeOffset.UtcNow,
            Payload = JsonSerializer.Serialize(new
            {
                annualCustomItems = normalized.AnnualCustomItems.Count,
                monthlyCustomItems = normalized.MonthlyCustomItems.Count,
                nameOverrides = normalized.NameOverrides.Count,
                hiddenAnnualApiRows = normalized.HiddenAnnualApiRows.Count,
                hiddenMonthlyApiRows = normalized.HiddenMonthlyApiRows.Count,
                oneTimeAnnualRows = normalized.OneTimeAnnualRows.Count
            })
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToPlannerCustomizationResponse(normalized);
    }

    public async Task<GeneralSettingsResponse> GetGeneralSettingsAsync(CancellationToken cancellationToken)
    {
        var state = await LoadUiStateAsync<GeneralSettingsState>(GeneralSettingsStateKey, cancellationToken);
        return new GeneralSettingsResponse(
            NormalizeCurrencyCode(state?.Currency),
            NormalizeThemeMode(state?.Theme));
    }

    public async Task<GeneralSettingsResponse> SaveGeneralSettingsAsync(
        UpdateGeneralSettingsRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var existing = await LoadUiStateAsync<GeneralSettingsState>(GeneralSettingsStateKey, cancellationToken)
            ?? new GeneralSettingsState();

        var currency = request.Currency is null
            ? NormalizeCurrencyCode(existing.Currency)
            : NormalizeCurrencyCode(request.Currency);
        var theme = request.Theme is null
            ? NormalizeThemeMode(existing.Theme)
            : NormalizeThemeMode(request.Theme);

        await UpsertUiStateEntryAsync(
            GeneralSettingsStateKey,
            new GeneralSettingsState
            {
                Currency = currency,
                Theme = theme
            },
            cancellationToken);

        dbContext.AuditEntries.Add(new AuditEntry
        {
            EntityType = "UiSettings",
            EntityId = Guid.NewGuid(),
            EventType = "GENERAL_SETTINGS_UPDATED",
            ChangedBy = actor,
            ChangedAt = DateTimeOffset.UtcNow,
            Payload = JsonSerializer.Serialize(new
            {
                currency,
                theme
            })
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new GeneralSettingsResponse(currency, theme);
    }

    public async Task<AnnualPlanResponse> GetAnnualPlanAsync(int year, CancellationToken cancellationToken)
    {
        ValidateYear(year);

        var categories = await dbContext.Categories
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        var cells = await dbContext.AnnualPlanCells
            .AsNoTracking()
            .Where(x => x.Year == year)
            .ToListAsync(cancellationToken);

        var cellsByKey = cells.ToDictionary(x => (x.CategoryId, x.Month));

        var rows = categories.Select(category =>
        {
            var months = Enumerable.Range(1, 12)
                .Select(month =>
                {
                    if (cellsByKey.TryGetValue((category.Id, month), out var cell))
                    {
                        return cell.PlannedAmount;
                    }

                    return 0m;
                })
                .ToArray();

            return new AnnualCategoryRowResponse(
                category.Id,
                category.Name,
                ToWireValue(category.Section),
                category.SortOrder,
                months,
                months.Sum());
        }).ToArray();

        var summary = BuildAnnualSummary(rows);

        return new AnnualPlanResponse(
            year,
            rows,
            summary);
    }

    public async Task UpsertAnnualPlanAsync(
        int year,
        UpdateAnnualPlanRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        ValidateYear(year);

        if (request.Cells.Count == 0)
        {
            return;
        }

        var categoryIds = request.Cells.Select(x => x.CategoryId).Distinct().ToArray();
        var categories = await dbContext.Categories
            .Where(x => categoryIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var missingCategoryIds = categoryIds.Except(categories).ToArray();
        if (missingCategoryIds.Length > 0)
        {
            throw new ValidationException($"Unknown category id(s): {string.Join(",", missingCategoryIds)}");
        }

        var existingCells = await dbContext.AnnualPlanCells
            .Where(x => x.Year == year && categoryIds.Contains(x.CategoryId))
            .ToListAsync(cancellationToken);

        var byKey = existingCells.ToDictionary(x => (x.CategoryId, x.Month));
        var now = DateTimeOffset.UtcNow;

        foreach (var input in request.Cells)
        {
            ValidateMonth(input.Month);
            var normalizedAmount = decimal.Round(input.PlannedAmount, 2, MidpointRounding.AwayFromZero);

            if (byKey.TryGetValue((input.CategoryId, input.Month), out var existing))
            {
                if (existing.PlannedAmount == normalizedAmount)
                {
                    continue;
                }

                var previous = existing.PlannedAmount;
                existing.PlannedAmount = normalizedAmount;
                existing.UpdatedAt = now;

                dbContext.AuditEntries.Add(new AuditEntry
                {
                    EntityType = "AnnualPlanCell",
                    EntityId = existing.Id,
                    EventType = "PLANNED_AMOUNT_UPDATED",
                    ChangedBy = actor,
                    ChangedAt = now,
                    Payload = JsonSerializer.Serialize(new
                    {
                        year,
                        month = input.Month,
                        categoryId = input.CategoryId,
                        previous,
                        current = normalizedAmount
                    })
                });

                continue;
            }

            var created = new AnnualPlanCell
            {
                Year = year,
                Month = input.Month,
                CategoryId = input.CategoryId,
                PlannedAmount = normalizedAmount,
                UpdatedAt = now
            };

            dbContext.AnnualPlanCells.Add(created);
            dbContext.AuditEntries.Add(new AuditEntry
            {
                EntityType = "AnnualPlanCell",
                EntityId = created.Id,
                EventType = "PLANNED_AMOUNT_CREATED",
                ChangedBy = actor,
                ChangedAt = now,
                Payload = JsonSerializer.Serialize(new
                {
                    year,
                    month = input.Month,
                    categoryId = input.CategoryId,
                    current = normalizedAmount
                })
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CopyAnnualPlanAsync(int targetYear, int sourceYear, string actor, CancellationToken cancellationToken)
    {
        ValidateYear(targetYear);
        ValidateYear(sourceYear);

        if (targetYear == sourceYear)
        {
            throw new ValidationException("Source and target year must differ.");
        }

        var sourceCells = await dbContext.AnnualPlanCells
            .Where(x => x.Year == sourceYear)
            .ToListAsync(cancellationToken);

        if (sourceCells.Count == 0)
        {
            throw new KeyNotFoundException($"No annual plan found for year {sourceYear}.");
        }

        var targetCells = await dbContext.AnnualPlanCells
            .Where(x => x.Year == targetYear)
            .ToListAsync(cancellationToken);

        dbContext.AnnualPlanCells.RemoveRange(targetCells);

        var now = DateTimeOffset.UtcNow;
        var copied = sourceCells.Select(x => new AnnualPlanCell
        {
            Year = targetYear,
            Month = x.Month,
            CategoryId = x.CategoryId,
            PlannedAmount = x.PlannedAmount,
            UpdatedAt = now
        }).ToArray();

        dbContext.AnnualPlanCells.AddRange(copied);

        dbContext.AuditEntries.Add(new AuditEntry
        {
            EntityType = "AnnualPlan",
            EntityId = Guid.NewGuid(),
            EventType = "YEAR_COPIED",
            ChangedBy = actor,
            ChangedAt = now,
            Payload = JsonSerializer.Serialize(new
            {
                sourceYear,
                targetYear,
                copiedCount = copied.Length,
                replacedCount = targetCells.Count
            })
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<GenerateMonthlyActionsResponse> GenerateMonthlyActionsAsync(
        int year,
        int month,
        string actor,
        CancellationToken cancellationToken)
    {
        ValidateYear(year);
        ValidateMonth(month);

        var monthlyCells = await dbContext.AnnualPlanCells
            .AsNoTracking()
            .Where(x => x.Year == year && x.Month == month)
            .ToDictionaryAsync(x => x.CategoryId, cancellationToken);

        var categoryIdsFromAnnual = monthlyCells.Keys.ToArray();
        var categories = await dbContext.Categories
            .AsNoTracking()
            .Where(x => categoryIdsFromAnnual.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var existingActions = await dbContext.MonthlyActions
            .Where(x => x.Year == year && x.Month == month)
            .ToDictionaryAsync(x => x.CategoryId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var created = 0;
        var updated = 0;

        foreach (var (categoryId, plannedCell) in monthlyCells)
        {
            if (!categories.TryGetValue(categoryId, out _))
            {
                continue;
            }

            var plannedAmount = plannedCell.PlannedAmount;

            if (!existingActions.TryGetValue(categoryId, out var action))
            {
                var createdAction = new MonthlyAction
                {
                    Year = year,
                    Month = month,
                    CategoryId = categoryId,
                    PlannedAmount = plannedAmount,
                    ActualAmount = null,
                    Status = MonthlyActionStatus.Planned,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                dbContext.MonthlyActions.Add(createdAction);
                dbContext.AuditEntries.Add(new AuditEntry
                {
                    EntityType = "MonthlyAction",
                    EntityId = createdAction.Id,
                    EventType = "MONTHLY_ACTION_CREATED",
                    ChangedBy = actor,
                    ChangedAt = now,
                    Payload = JsonSerializer.Serialize(new
                    {
                        year,
                        month,
                        categoryId,
                        plannedAmount
                    })
                });

                created++;
                continue;
            }

            if (action.PlannedAmount == plannedAmount)
            {
                continue;
            }

            var previous = action.PlannedAmount;
            action.PlannedAmount = plannedAmount;
            action.UpdatedAt = now;
            updated++;

            dbContext.AuditEntries.Add(new AuditEntry
            {
                EntityType = "MonthlyAction",
                EntityId = action.Id,
                EventType = "MONTHLY_PLANNED_UPDATED",
                ChangedBy = actor,
                ChangedAt = now,
                Payload = JsonSerializer.Serialize(new
                {
                    year,
                    month,
                    categoryId,
                    previous,
                    current = plannedAmount
                })
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new GenerateMonthlyActionsResponse(created, updated, monthlyCells.Count);
    }

    public async Task<MonthlyWorkspaceResponse> GetMonthlyWorkspaceAsync(
        int year,
        int month,
        string? statusFilter,
        CancellationToken cancellationToken)
    {
        ValidateYear(year);
        ValidateMonth(month);

        var normalizedFilter = NormalizeStatusFilter(statusFilter);

        var allActions = await dbContext.MonthlyActions
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.Year == year && x.Month == month)
            .OrderBy(x => x.Category.SortOrder)
            .ToListAsync(cancellationToken);

        var filtered = normalizedFilter is null
            ? allActions
            : allActions.Where(x => x.Status == normalizedFilter.Value).ToList();

        var actions = filtered.Select(x => new MonthlyActionResponse(
            x.Id,
            x.CategoryId,
            x.Category.Name,
            ToWireValue(x.Category.Section),
            x.PlannedAmount,
            x.ActualAmount,
            ToWireValue(x.Status),
            x.UpdatedAt)).ToArray();

        var summary = BuildMonthlySummary(allActions);
        var completion = new MonthlyCompletionResponse(
            Planned: allActions.Count(x => x.Status == MonthlyActionStatus.Planned),
            Done: allActions.Count(x => x.Status == MonthlyActionStatus.Done),
            Partial: allActions.Count(x => x.Status == MonthlyActionStatus.Partial),
            Skipped: allActions.Count(x => x.Status == MonthlyActionStatus.Skipped),
            Total: allActions.Count);

        return new MonthlyWorkspaceResponse(
            year,
            month,
            normalizedFilter.HasValue ? ToWireValue(normalizedFilter.Value) : "ALL",
            summary,
            completion,
            actions);
    }

    public async Task<MonthlyActionResponse> UpdateMonthlyActionAsync(
        int year,
        int month,
        Guid actionId,
        UpdateMonthlyActionRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        ValidateYear(year);
        ValidateMonth(month);

        var action = await dbContext.MonthlyActions
            .Include(x => x.Category)
            .SingleOrDefaultAsync(
                x => x.Id == actionId && x.Year == year && x.Month == month,
                cancellationToken);

        if (action is null)
        {
            throw new KeyNotFoundException("Monthly action was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        var statusChanged = false;
        var amountChanged = false;

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<MonthlyActionStatus>(request.Status, true, out var parsedStatus))
            {
                throw new ValidationException($"Unsupported status '{request.Status}'.");
            }

            if (action.Status != parsedStatus)
            {
                var previous = action.Status;
                action.Status = parsedStatus;
                statusChanged = true;

                dbContext.AuditEntries.Add(new AuditEntry
                {
                    EntityType = "MonthlyAction",
                    EntityId = action.Id,
                    EventType = "STATUS_UPDATED",
                    ChangedBy = actor,
                    ChangedAt = now,
                    Payload = JsonSerializer.Serialize(new
                    {
                        year,
                        month,
                        previous = ToWireValue(previous),
                        current = ToWireValue(parsedStatus)
                    })
                });
            }
        }

        if (request.ActualAmount.HasValue)
        {
            var normalized = decimal.Round(request.ActualAmount.Value, 2, MidpointRounding.AwayFromZero);
            if (action.ActualAmount != normalized)
            {
                var previous = action.ActualAmount;
                action.ActualAmount = normalized;
                amountChanged = true;

                dbContext.AuditEntries.Add(new AuditEntry
                {
                    EntityType = "MonthlyAction",
                    EntityId = action.Id,
                    EventType = "ACTUAL_AMOUNT_UPDATED",
                    ChangedBy = actor,
                    ChangedAt = now,
                    Payload = JsonSerializer.Serialize(new
                    {
                        year,
                        month,
                        previous,
                        current = normalized
                    })
                });
            }
        }

        if (action.Status == MonthlyActionStatus.Done && !action.ActualAmount.HasValue)
        {
            action.ActualAmount = action.PlannedAmount;
            amountChanged = true;
        }

        if (action.Status == MonthlyActionStatus.Skipped)
        {
            action.ActualAmount = 0m;
        }

        if (statusChanged || amountChanged)
        {
            action.UpdatedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new MonthlyActionResponse(
            action.Id,
            action.CategoryId,
            action.Category.Name,
            ToWireValue(action.Category.Section),
            action.PlannedAmount,
            action.ActualAmount,
            ToWireValue(action.Status),
            action.UpdatedAt);
    }

    public async Task<IReadOnlyList<AuditEntryResponse>> GetAuditAsync(
        int? year,
        int? month,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit <= 0 || limit > 200)
        {
            limit = 50;
        }

        IQueryable<AuditEntry> query = dbContext.AuditEntries.AsNoTracking();

        if (year.HasValue || month.HasValue)
        {
            query = query.Where(entry =>
                (!year.HasValue || EF.Functions.ILike(entry.Payload, $"%\"year\":{year.Value}%")) &&
                (!month.HasValue || EF.Functions.ILike(entry.Payload, $"%\"month\":{month.Value}%")));
        }

        var entries = await query
            .OrderByDescending(x => x.ChangedAt)
            .Take(limit)
            .Select(x => new AuditEntryResponse(
                x.Id,
                x.ChangedAt,
                x.EntityType,
                x.EntityId,
                x.EventType,
                x.ChangedBy,
                x.Payload))
            .ToListAsync(cancellationToken);

        return entries;
    }

    public async Task<AssetsOverviewResponse> GetAssetsOverviewAsync(int year, int month, CancellationToken cancellationToken)
    {
        ValidateYear(year);
        ValidateMonth(month);

        var generalSettings = await LoadUiStateAsync<GeneralSettingsState>(GeneralSettingsStateKey, cancellationToken);
        var baseCurrency = NormalizeCurrencyCode(generalSettings?.Currency);

        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .OrderBy(x => x.IsArchived)
            .ThenBy(x => x.Kind)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var snapshots = await dbContext.AccountSnapshots
            .AsNoTracking()
            .Include(x => x.Account)
            .Where(x => x.Year == year && x.Month == month)
            .OrderBy(x => x.Account.Name)
            .ToListAsync(cancellationToken);

        var transfers = await dbContext.AccountTransfers
            .AsNoTracking()
            .Include(x => x.FromAccount)
            .Include(x => x.ToAccount)
            .OrderByDescending(x => x.TransferDate)
            .Take(50)
            .ToListAsync(cancellationToken);

        var holdings = await dbContext.InvestmentHoldings
            .AsNoTracking()
            .Include(x => x.Account)
            .OrderBy(x => x.Symbol)
            .ToListAsync(cancellationToken);

        var savingsGoals = await dbContext.SavingsGoals
            .AsNoTracking()
            .Include(x => x.Account)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var accountResponses = accounts.Select(ToAccountResponse).ToArray();
        var accountById = accounts.ToDictionary(x => x.Id);
        var holdingResponses = holdings.Select(ToHoldingResponse).ToArray();
        var investments = BuildInvestmentsDashboard(holdingResponses);
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
        var summary = new AssetsOverviewSummaryResponse(
            BaseCurrency: baseCurrency,
            NetWorth: netWorth,
            SnapshotPlanned: snapshotPlanned,
            SnapshotActual: snapshotActual,
            ExchangeRates: ratesWithBase);

        return new AssetsOverviewResponse(
            Year: year,
            Month: month,
            Summary: summary,
            Accounts: accountResponses,
            Transfers: transfers.Select(x => new AccountTransferResponse(
                x.Id,
                x.FromAccountId,
                x.FromAccount.Name,
                x.ToAccountId,
                x.ToAccount.Name,
                x.Amount,
                x.Note,
                x.TransferDate)).ToArray(),
            Snapshots: snapshots.Select(x => new AccountSnapshotResponse(
                x.AccountId,
                x.Account.Name,
                ToWireValue(x.Account.Kind),
                x.Year,
                x.Month,
                x.PlannedBalance,
                x.ActualBalance,
                x.UpdatedAt)).ToArray(),
            Holdings: holdingResponses,
            Investments: investments,
            SavingsGoals: savingsGoals.Select(x => ToSavingsGoalResponse(x, accountById)).ToArray());
    }

    public async Task<BudgetAccountResponse> CreateAccountAsync(
        CreateBudgetAccountRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Account name is required.");
        }

        var kind = NormalizeAccountKind(request.Kind);
        var currency = NormalizeCurrencyCode(request.Currency);
        var now = DateTimeOffset.UtcNow;

        var account = new BudgetAccount
        {
            Name = request.Name.Trim(),
            Kind = kind,
            Currency = currency,
            CurrentBalance = decimal.Round(request.InitialBalance, 2, MidpointRounding.AwayFromZero),
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Accounts.Add(account);
        dbContext.AuditEntries.Add(new AuditEntry
        {
            EntityType = "BudgetAccount",
            EntityId = account.Id,
            EventType = "ACCOUNT_CREATED",
            ChangedBy = actor,
            ChangedAt = now,
            Payload = JsonSerializer.Serialize(new
            {
                account.Name,
                kind = ToWireValue(account.Kind),
                account.Currency,
                account.CurrentBalance
            })
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToAccountResponse(account);
    }

    public async Task<BudgetAccountResponse> UpdateAccountAsync(
        Guid accountId,
        UpdateBudgetAccountRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == accountId, cancellationToken);
        if (account is null)
        {
            throw new KeyNotFoundException("Account was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        var changed = false;

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            account.Name = request.Name.Trim();
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(request.Kind))
        {
            account.Kind = NormalizeAccountKind(request.Kind);
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(request.Currency))
        {
            account.Currency = NormalizeCurrencyCode(request.Currency);
            changed = true;
        }

        if (request.CurrentBalance.HasValue)
        {
            account.CurrentBalance = decimal.Round(request.CurrentBalance.Value, 2, MidpointRounding.AwayFromZero);
            changed = true;
        }

        if (request.IsArchived.HasValue)
        {
            account.IsArchived = request.IsArchived.Value;
            changed = true;
        }

        if (changed)
        {
            account.UpdatedAt = now;
            dbContext.AuditEntries.Add(new AuditEntry
            {
                EntityType = "BudgetAccount",
                EntityId = account.Id,
                EventType = "ACCOUNT_UPDATED",
                ChangedBy = actor,
                ChangedAt = now,
                Payload = JsonSerializer.Serialize(new
                {
                    account.Name,
                    kind = ToWireValue(account.Kind),
                    account.Currency,
                    account.CurrentBalance,
                    account.IsArchived
                })
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ToAccountResponse(account);
    }

    public async Task<AccountTransferResponse> CreateTransferAsync(
        CreateAccountTransferRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (request.FromAccountId == request.ToAccountId)
        {
            throw new ValidationException("Transfer must use two different accounts.");
        }

        if (request.Amount <= 0)
        {
            throw new ValidationException("Transfer amount must be greater than 0.");
        }

        var accountIds = new[] { request.FromAccountId, request.ToAccountId };
        var accounts = await dbContext.Accounts
            .Where(x => accountIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        if (!accounts.TryGetValue(request.FromAccountId, out var fromAccount) ||
            !accounts.TryGetValue(request.ToAccountId, out var toAccount))
        {
            throw new KeyNotFoundException("Transfer account was not found.");
        }

        if (fromAccount.IsArchived || toAccount.IsArchived)
        {
            throw new ValidationException("Cannot transfer from or to archived account.");
        }

        if (!string.Equals(fromAccount.Currency, toAccount.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Transfers between different currencies are not supported.");
        }

        var amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        fromAccount.CurrentBalance = decimal.Round(fromAccount.CurrentBalance - amount, 2, MidpointRounding.AwayFromZero);
        toAccount.CurrentBalance = decimal.Round(toAccount.CurrentBalance + amount, 2, MidpointRounding.AwayFromZero);

        var now = DateTimeOffset.UtcNow;
        fromAccount.UpdatedAt = now;
        toAccount.UpdatedAt = now;

        var transfer = new AccountTransfer
        {
            FromAccountId = fromAccount.Id,
            ToAccountId = toAccount.Id,
            Amount = amount,
            Note = request.Note?.Trim() ?? string.Empty,
            TransferDate = request.TransferDate ?? now,
            CreatedAt = now
        };

        dbContext.AccountTransfers.Add(transfer);
        dbContext.AuditEntries.Add(new AuditEntry
        {
            EntityType = "AccountTransfer",
            EntityId = transfer.Id,
            EventType = "TRANSFER_CREATED",
            ChangedBy = actor,
            ChangedAt = now,
            Payload = JsonSerializer.Serialize(new
            {
                transfer.FromAccountId,
                transfer.ToAccountId,
                transfer.Amount,
                transfer.TransferDate,
                transfer.Note
            })
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AccountTransferResponse(
            transfer.Id,
            transfer.FromAccountId,
            fromAccount.Name,
            transfer.ToAccountId,
            toAccount.Name,
            transfer.Amount,
            transfer.Note,
            transfer.TransferDate);
    }

    public async Task<IReadOnlyList<AccountSnapshotResponse>> UpsertAccountSnapshotsAsync(
        int year,
        int month,
        UpsertMonthlyAccountSnapshotsRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        ValidateYear(year);
        ValidateMonth(month);

        if (request.Snapshots.Count == 0)
        {
            return [];
        }

        var accountIds = request.Snapshots.Select(x => x.AccountId).Distinct().ToArray();
        var accounts = await dbContext.Accounts
            .Where(x => accountIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var missing = accountIds.Where(x => !accounts.ContainsKey(x)).ToArray();
        if (missing.Length > 0)
        {
            throw new ValidationException($"Unknown account id(s): {string.Join(",", missing)}");
        }

        var existing = await dbContext.AccountSnapshots
            .Where(x => x.Year == year && x.Month == month && accountIds.Contains(x.AccountId))
            .ToDictionaryAsync(x => x.AccountId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var changed = 0;

        foreach (var input in request.Snapshots)
        {
            var planned = decimal.Round(input.PlannedBalance, 2, MidpointRounding.AwayFromZero);
            var actual = input.ActualBalance.HasValue
                ? (decimal?)decimal.Round(input.ActualBalance.Value, 2, MidpointRounding.AwayFromZero)
                : null;

            if (existing.TryGetValue(input.AccountId, out var snapshot))
            {
                if (snapshot.PlannedBalance == planned && snapshot.ActualBalance == actual)
                {
                    continue;
                }

                snapshot.PlannedBalance = planned;
                snapshot.ActualBalance = actual;
                snapshot.UpdatedAt = now;
                changed++;
                continue;
            }

            dbContext.AccountSnapshots.Add(new AccountSnapshot
            {
                AccountId = input.AccountId,
                Year = year,
                Month = month,
                PlannedBalance = planned,
                ActualBalance = actual,
                UpdatedAt = now
            });
            changed++;
        }

        if (changed > 0)
        {
            dbContext.AuditEntries.Add(new AuditEntry
            {
                EntityType = "AccountSnapshot",
                EntityId = Guid.NewGuid(),
                EventType = "MONTHLY_SNAPSHOTS_UPSERTED",
                ChangedBy = actor,
                ChangedAt = now,
                Payload = JsonSerializer.Serialize(new
                {
                    year,
                    month,
                    changed
                })
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var result = await dbContext.AccountSnapshots
            .AsNoTracking()
            .Include(x => x.Account)
            .Where(x => x.Year == year && x.Month == month)
            .OrderBy(x => x.Account.Name)
            .Select(x => new AccountSnapshotResponse(
                x.AccountId,
                x.Account.Name,
                ToWireValue(x.Account.Kind),
                x.Year,
                x.Month,
                x.PlannedBalance,
                x.ActualBalance,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return result;
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
        var fetchedPrice = await FetchMarketPriceFromProviderAsync(symbol, cancellationToken);
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
        var pricesBySymbol = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);

        foreach (var holding in holdings)
        {
            symbols.Add(holding.Symbol);
            if (!pricesBySymbol.ContainsKey(holding.Symbol))
            {
                pricesBySymbol[holding.Symbol] = await FetchMarketPriceFromProviderAsync(holding.Symbol, cancellationToken);
            }

            var fetchedPrice = pricesBySymbol[holding.Symbol];
            if (!fetchedPrice.HasValue)
            {
                continue;
            }

            var nextPrice = fetchedPrice.Value;

            if (holding.LastFetchedPrice == nextPrice)
            {
                continue;
            }

            holding.LastFetchedPrice = nextPrice;
            holding.LastPriceUpdatedAt = now;
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

    public async Task<SavingsGoalResponse> CreateSavingsGoalAsync(
        CreateSavingsGoalRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Savings goal name is required.");
        }

        if (request.TargetAmount <= 0)
        {
            throw new ValidationException("Target amount must be greater than 0.");
        }

        ValidateSavingsGoalDate(request.TargetYear, request.TargetMonth);

        BudgetAccount? linkedAccount = null;
        if (request.AccountId.HasValue)
        {
            linkedAccount = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == request.AccountId.Value, cancellationToken);
            if (linkedAccount is null)
            {
                throw new KeyNotFoundException("Linked account was not found.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var goal = new SavingsGoal
        {
            Name = request.Name.Trim(),
            AccountId = request.AccountId,
            TargetAmount = decimal.Round(request.TargetAmount, 2, MidpointRounding.AwayFromZero),
            CurrentAmount = decimal.Round(request.CurrentAmount, 2, MidpointRounding.AwayFromZero),
            MonthlyContributionTarget = decimal.Round(request.MonthlyContributionTarget, 2, MidpointRounding.AwayFromZero),
            TargetYear = request.TargetYear,
            TargetMonth = request.TargetMonth,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.SavingsGoals.Add(goal);
        dbContext.AuditEntries.Add(new AuditEntry
        {
            EntityType = "SavingsGoal",
            EntityId = goal.Id,
            EventType = "GOAL_CREATED",
            ChangedBy = actor,
            ChangedAt = now,
            Payload = JsonSerializer.Serialize(new
            {
                goal.Name,
                goal.TargetAmount,
                goal.MonthlyContributionTarget
            })
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        goal.Account = linkedAccount;

        return ToSavingsGoalResponse(
            goal,
            linkedAccount is null ? new Dictionary<Guid, BudgetAccount>() : new Dictionary<Guid, BudgetAccount> { [linkedAccount.Id] = linkedAccount });
    }

    public async Task<SavingsGoalResponse> UpdateSavingsGoalAsync(
        Guid goalId,
        UpdateSavingsGoalRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var goal = await dbContext.SavingsGoals
            .Include(x => x.Account)
            .SingleOrDefaultAsync(x => x.Id == goalId, cancellationToken);

        if (goal is null)
        {
            throw new KeyNotFoundException("Savings goal was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        var changed = false;

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            goal.Name = request.Name.Trim();
            changed = true;
        }

        if (request.AccountId.HasValue)
        {
            var account = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == request.AccountId.Value, cancellationToken);
            if (account is null)
            {
                throw new KeyNotFoundException("Linked account was not found.");
            }

            goal.AccountId = account.Id;
            goal.Account = account;
            changed = true;
        }

        if (request.TargetAmount.HasValue)
        {
            if (request.TargetAmount <= 0)
            {
                throw new ValidationException("Target amount must be greater than 0.");
            }

            goal.TargetAmount = decimal.Round(request.TargetAmount.Value, 2, MidpointRounding.AwayFromZero);
            changed = true;
        }

        if (request.CurrentAmount.HasValue)
        {
            goal.CurrentAmount = decimal.Round(request.CurrentAmount.Value, 2, MidpointRounding.AwayFromZero);
            changed = true;
        }

        if (request.MonthlyContributionTarget.HasValue)
        {
            goal.MonthlyContributionTarget = decimal.Round(request.MonthlyContributionTarget.Value, 2, MidpointRounding.AwayFromZero);
            changed = true;
        }

        if (request.ClearTargetDate)
        {
            goal.TargetYear = null;
            goal.TargetMonth = null;
            changed = true;
        }
        else if (request.TargetYear.HasValue || request.TargetMonth.HasValue)
        {
            var targetYear = request.TargetYear ?? goal.TargetYear;
            var targetMonth = request.TargetMonth ?? goal.TargetMonth;
            ValidateSavingsGoalDate(targetYear, targetMonth);
            goal.TargetYear = targetYear;
            goal.TargetMonth = targetMonth;
            changed = true;
        }

        if (changed)
        {
            goal.UpdatedAt = now;
            dbContext.AuditEntries.Add(new AuditEntry
            {
                EntityType = "SavingsGoal",
                EntityId = goal.Id,
                EventType = "GOAL_UPDATED",
                ChangedBy = actor,
                ChangedAt = now,
                Payload = JsonSerializer.Serialize(new
                {
                    goal.Name,
                    goal.TargetAmount,
                    goal.CurrentAmount,
                    goal.MonthlyContributionTarget,
                    goal.TargetYear,
                    goal.TargetMonth
                })
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        Dictionary<Guid, BudgetAccount> accountMap = [];
        if (goal.Account is not null)
        {
            accountMap[goal.Account.Id] = goal.Account;
        }

        return ToSavingsGoalResponse(goal, accountMap);
    }

    private async Task<T?> LoadUiStateAsync<T>(string stateKey, CancellationToken cancellationToken)
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

    private async Task UpsertUiStateEntryAsync<T>(
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

    private static PlannerCustomizationResponse ToPlannerCustomizationResponse(PlannerCustomizationState state)
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

    private static List<ManagedSectionStateItem> NormalizeManagedSections(IEnumerable<ManagedSectionStateItem>? sections)
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

    private static PlannerCustomizationState NormalizePlannerCustomization(PlannerCustomizationState? state)
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

    private static BudgetAccountResponse ToAccountResponse(BudgetAccount account)
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

    private static InvestmentHoldingResponse ToHoldingResponse(InvestmentHolding holding)
    {
        var effectivePrice = holding.ManualPriceOverride ?? holding.LastFetchedPrice;
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
            holding.LastFetchedPrice,
            effectivePrice,
            currentValue,
            costBasis,
            profitLoss,
            holding.LastPriceUpdatedAt);
    }

    private static InvestmentsDashboardResponse BuildInvestmentsDashboard(
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

    private async Task<Dictionary<string, decimal>> ResolveExchangeRatesAsync(
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

    private async Task<decimal> FetchExchangeRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken)
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

    private static SavingsGoalResponse ToSavingsGoalResponse(
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

    private static string NormalizeSectionKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return "PERSONAL_EXPENSES";
        }

        var normalized = kind.Trim().ToUpperInvariant();
        return ManagedSectionKinds.Contains(normalized) ? normalized : "PERSONAL_EXPENSES";
    }

    private static BudgetAccountKind NormalizeAccountKind(string? kind)
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

    private static string NormalizeMonthlyStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "PLANNED";
        }

        var normalized = status.Trim().ToUpperInvariant();
        return MonthlyStatuses.Contains(normalized) ? normalized : "PLANNED";
    }

    private static string NormalizeCurrencyCode(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return "PLN";
        }

        var normalized = currency.Trim().ToUpperInvariant();
        return SupportedCurrencies.Contains(normalized) ? normalized : "PLN";
    }

    private static string NormalizeThemeMode(string? theme)
    {
        if (string.IsNullOrWhiteSpace(theme))
        {
            return "LIGHT";
        }

        var normalized = theme.Trim().ToUpperInvariant();
        return SupportedThemes.Contains(normalized) ? normalized : "LIGHT";
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

    private async Task<decimal?> FetchMarketPriceFromProviderAsync(string symbol, CancellationToken cancellationToken)
    {
        var normalized = NormalizeSymbol(symbol);

        try
        {
            var client = httpClientFactory.CreateClient("market-prices");
            client.Timeout = TimeSpan.FromSeconds(5);
            var endpoint = $"https://stooq.com/q/l/?s={normalized.ToLowerInvariant()}&i=d";
            var csv = await client.GetStringAsync(endpoint, cancellationToken);
            return ParseStooqClosePrice(csv);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to fetch market price for symbol {Symbol}.", normalized);
            return null;
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

    private static void ValidateSavingsGoalDate(int? targetYear, int? targetMonth)
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

    private sealed class FrankfurterResponse
    {
        public Dictionary<string, decimal> Rates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateYear(int year)
    {
        if (year < 2000 || year > 2100)
        {
            throw new ValidationException($"Unsupported year '{year}'.");
        }
    }

    private static void ValidateMonth(int month)
    {
        if (month < 1 || month > 12)
        {
            throw new ValidationException($"Unsupported month '{month}'.");
        }
    }

    private static MonthlyActionStatus? NormalizeStatusFilter(string? status)
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

    private static AnnualSummaryResponse BuildAnnualSummary(IEnumerable<AnnualCategoryRowResponse> rows)
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

    private static MonthlySummaryCardsResponse BuildMonthlySummary(IReadOnlyCollection<MonthlyAction> actions)
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

    private static decimal ResolveActualAmount(MonthlyAction action)
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

    private sealed class ManagedSectionsState
    {
        public List<ManagedSectionStateItem> Sections { get; set; } = [];
    }

    private sealed class ManagedSectionStateItem
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

    private sealed class PlannerCustomizationState
    {
        public List<AnnualCustomItemState> AnnualCustomItems { get; set; } = [];
        public List<MonthlyCustomItemState> MonthlyCustomItems { get; set; } = [];
        public Dictionary<string, string> NameOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> HiddenAnnualApiRows { get; set; } = [];
        public List<string> HiddenMonthlyApiRows { get; set; } = [];
        public List<string> OneTimeAnnualRows { get; set; } = [];
    }

    private sealed class AnnualCustomItemState
    {
        public string Id { get; set; } = string.Empty;
        public int Year { get; set; }
        public string SectionId { get; set; } = string.Empty;
        public string SectionKind { get; set; } = "PERSONAL_EXPENSES";
        public string Name { get; set; } = string.Empty;
        public List<decimal> Months { get; set; } = [];
    }

    private sealed class MonthlyCustomItemState
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

    private sealed class GeneralSettingsState
    {
        public string Currency { get; set; } = "PLN";
        public string Theme { get; set; } = "LIGHT";
    }
}
