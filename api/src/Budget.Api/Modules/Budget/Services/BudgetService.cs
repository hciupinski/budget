using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using Budget.Api.Infrastructure.Persistence;
using Budget.Api.Modules.Budget.Contracts;
using Budget.Api.Modules.Budget.Domain;
using Microsoft.EntityFrameworkCore;

namespace Budget.Api.Modules.Budget.Services;

public sealed class BudgetService(BudgetDbContext dbContext)
{
    private const string SectionsStateKey = "managed_sections";
    private const string PlannerCustomizationStateKey = "planner_customization";

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
                Status = x.Status
            }).ToList() ?? [],
            NameOverrides = request.NameOverrides?.ToDictionary(x => x.Key, x => x.Value) ?? [],
            HiddenAnnualApiRows = request.HiddenAnnualApiRows?.ToList() ?? [],
            HiddenMonthlyApiRows = request.HiddenMonthlyApiRows?.ToList() ?? []
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
                hiddenMonthlyApiRows = normalized.HiddenMonthlyApiRows.Count
            })
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToPlannerCustomizationResponse(normalized);
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
                x.Status)).ToArray(),
            state.NameOverrides,
            state.HiddenAnnualApiRows,
            state.HiddenMonthlyApiRows);
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
                Status = NormalizeMonthlyStatus(item.Status)
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

        return new PlannerCustomizationState
        {
            AnnualCustomItems = annual,
            MonthlyCustomItems = monthly,
            NameOverrides = overrides,
            HiddenAnnualApiRows = hiddenAnnual,
            HiddenMonthlyApiRows = hiddenMonthly
        };
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

    private static string NormalizeMonthlyStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "PLANNED";
        }

        var normalized = status.Trim().ToUpperInvariant();
        return MonthlyStatuses.Contains(normalized) ? normalized : "PLANNED";
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
    }
}
