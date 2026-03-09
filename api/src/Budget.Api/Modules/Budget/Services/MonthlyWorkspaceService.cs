using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Budget.Api.Infrastructure.Persistence;
using Budget.Api.Modules.Budget.Contracts;
using Budget.Api.Modules.Budget.Domain;
using Microsoft.EntityFrameworkCore;

namespace Budget.Api.Modules.Budget.Services;

public sealed class MonthlyWorkspaceService(
    BudgetDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IMarketPriceService marketPriceService,
    ILogger<MonthlyWorkspaceService> logger)
    : BudgetServiceBase(dbContext, httpClientFactory, marketPriceService, logger),
      IMonthlyWorkspaceService
{
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
}
