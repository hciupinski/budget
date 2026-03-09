using System.Text.Json;
using Budget.Api.Infrastructure.Persistence;
using Budget.Api.Modules.Budget.Contracts;
using Budget.Api.Modules.Budget.Domain;
using Microsoft.EntityFrameworkCore;

namespace Budget.Api.Modules.Budget.Services;

public sealed class SettingsService(
    BudgetDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IMarketPriceService marketPriceService,
    ILogger<SettingsService> logger)
    : BudgetServiceBase(dbContext, httpClientFactory, marketPriceService, logger),
      ISettingsService
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
}
