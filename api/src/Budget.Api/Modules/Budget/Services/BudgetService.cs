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

        var categories = await dbContext.Categories
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        var monthlyCells = await dbContext.AnnualPlanCells
            .AsNoTracking()
            .Where(x => x.Year == year && x.Month == month)
            .ToDictionaryAsync(x => x.CategoryId, cancellationToken);

        var existingActions = await dbContext.MonthlyActions
            .Where(x => x.Year == year && x.Month == month)
            .ToDictionaryAsync(x => x.CategoryId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var created = 0;
        var updated = 0;

        foreach (var category in categories)
        {
            var plannedAmount = monthlyCells.TryGetValue(category.Id, out var plannedCell)
                ? plannedCell.PlannedAmount
                : 0m;

            if (!existingActions.TryGetValue(category.Id, out var action))
            {
                var createdAction = new MonthlyAction
                {
                    Year = year,
                    Month = month,
                    CategoryId = category.Id,
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
                        categoryId = category.Id,
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
                    categoryId = category.Id,
                    previous,
                    current = plannedAmount
                })
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new GenerateMonthlyActionsResponse(created, updated, categories.Count);
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
}
