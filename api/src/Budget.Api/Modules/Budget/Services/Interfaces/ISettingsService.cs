using Budget.Api.Modules.Budget.Contracts;

namespace Budget.Api.Modules.Budget.Services;

public interface ISettingsService
{
    Task<IReadOnlyList<BudgetCategoryResponse>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<ManagedSectionsResponse> GetManagedSectionsAsync(CancellationToken cancellationToken);
    Task<ManagedSectionsResponse> SaveManagedSectionsAsync(
        UpdateManagedSectionsRequest request,
        string actor,
        CancellationToken cancellationToken);
    Task<PlannerCustomizationResponse> GetPlannerCustomizationAsync(CancellationToken cancellationToken);
    Task<PlannerCustomizationResponse> SavePlannerCustomizationAsync(
        UpdatePlannerCustomizationRequest request,
        string actor,
        CancellationToken cancellationToken);
    Task<GeneralSettingsResponse> GetGeneralSettingsAsync(CancellationToken cancellationToken);
    Task<GeneralSettingsResponse> SaveGeneralSettingsAsync(
        UpdateGeneralSettingsRequest request,
        string actor,
        CancellationToken cancellationToken);
}
