using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Budget.Api.Infrastructure.Persistence;
using Budget.Api.Modules.Budget.Contracts;
using Budget.Api.Modules.Budget.Domain;
using Microsoft.EntityFrameworkCore;

namespace Budget.Api.Modules.Budget.Services;

public sealed class AnnualPlanService(
    BudgetDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IMarketPriceService marketPriceService,
    ILogger<AnnualPlanService> logger)
    : BudgetServiceBase(dbContext, httpClientFactory, marketPriceService, logger),
      IAnnualPlanService
{
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

        var duplicateCells = request.Cells
            .GroupBy(x => (x.CategoryId, x.Month))
            .Where(x => x.Count() > 1)
            .Select(x => $"{x.Key.CategoryId}:{x.Key.Month}")
            .ToArray();
        if (duplicateCells.Length > 0)
        {
            throw new ValidationException(
                $"Duplicate annual plan cells are not allowed. Duplicates: {string.Join(", ", duplicateCells)}");
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
}
