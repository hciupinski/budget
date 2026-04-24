using System.ComponentModel.DataAnnotations;

namespace Budget.Api.Modules.Budget.Contracts.Projects;

public static class ProjectContracts
{
    public sealed record ProjectsListResponse(
        IReadOnlyList<ProjectSummaryResponse> Projects);

    public sealed record ProjectSummaryResponse(
        Guid Id,
        string Name,
        string Currency,
        bool IsArchived,
        ProjectTotalsResponse Totals,
        ProjectCompletionSummaryResponse Completion,
        DateTimeOffset UpdatedAt);

    public sealed record ProjectDetailResponse(
        Guid Id,
        string Name,
        string Description,
        string Currency,
        int SortOrder,
        bool IsArchived,
        DateTimeOffset? ArchivedAt,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        ProjectTotalsResponse Totals,
        ProjectCompletionSummaryResponse Completion,
        IReadOnlyList<ProjectMilestoneResponse> Milestones);

    public sealed record ProjectTotalsResponse(
        decimal Planned,
        decimal Paid,
        decimal ManualAdjustment,
        decimal Actual,
        decimal Variance);

    public sealed record ProjectCompletionSummaryResponse(
        int ActiveMilestones,
        int CompletedMilestones,
        int ActiveSteps,
        int CompletedSteps,
        int OpenItems,
        int DoneItems);

    public sealed record ProjectMilestoneResponse(
        Guid Id,
        string Name,
        int SortOrder,
        string CompletionStatus,
        string CompletionSource,
        DateTimeOffset? CompletedAt,
        ProjectTotalsResponse Totals,
        ProjectCompletionSummaryResponse Completion,
        IReadOnlyList<ProjectStepResponse> Steps);

    public sealed record ProjectStepResponse(
        Guid Id,
        string Name,
        int SortOrder,
        string CompletionStatus,
        string CompletionSource,
        DateTimeOffset? CompletedAt,
        ProjectTotalsResponse Totals,
        ProjectCompletionSummaryResponse Completion,
        IReadOnlyList<ProjectItemResponse> Items);

    public sealed record ProjectItemResponse(
        Guid Id,
        string Name,
        int SortOrder,
        decimal PlannedAmount,
        decimal PaidAmount,
        decimal ManualAdjustment,
        decimal ActualAmount,
        decimal Variance,
        bool IsDone,
        DateTimeOffset? DoneAt,
        IReadOnlyList<ProjectPaymentResponse> Payments,
        IReadOnlyList<ProjectAttachmentResponse> Attachments);

    public sealed record ProjectPaymentResponse(
        Guid Id,
        decimal Amount,
        DateTimeOffset PaymentDate,
        string Note,
        bool IsArchived,
        DateTimeOffset UpdatedAt);

    public sealed record ProjectAttachmentResponse(
        Guid Id,
        Guid ItemId,
        Guid? PaymentId,
        string Kind,
        string MimeType,
        long SizeBytes,
        string OriginalName,
        DateTimeOffset CreatedAt,
        bool IsRemoved,
        DateTimeOffset? RemovedAt,
        string DownloadPath);

    public sealed record CreateProjectRequest(
        [property: Required] string Name,
        string? Description,
        [property: Required] string Currency,
        int SortOrder);

    public sealed record UpdateProjectRequest(
        string? Name,
        string? Description,
        string? Currency,
        int? SortOrder);

    public sealed record CreateMilestoneRequest(
        [property: Required] string Name,
        int SortOrder);

    public sealed record UpdateMilestoneRequest(
        string? Name,
        int? SortOrder);

    public sealed record UpdateMilestoneCompletionRequest(
        [property: Required] string Source,
        string? Status);

    public sealed record CreateStepRequest(
        [property: Required] string Name,
        int SortOrder);

    public sealed record UpdateStepRequest(
        string? Name,
        int? SortOrder);

    public sealed record UpdateStepCompletionRequest(
        [property: Required] string Source,
        string? Status);

    public sealed record CreateItemRequest(
        [property: Required] string Name,
        decimal PlannedAmount,
        decimal ManualAdjustment,
        bool IsDone,
        int SortOrder);

    public sealed record UpdateItemRequest(
        string? Name,
        decimal? PlannedAmount,
        decimal? ManualAdjustment,
        bool? IsDone,
        int? SortOrder);

    public sealed record CreatePaymentRequest(
        decimal Amount,
        DateTimeOffset? PaymentDate,
        string? Note);

    public sealed record UpdatePaymentRequest(
        decimal? Amount,
        DateTimeOffset? PaymentDate,
        string? Note);

    public sealed record UploadAttachmentRequest(
        [property: Required] string Kind,
        Guid? PaymentId);
}
