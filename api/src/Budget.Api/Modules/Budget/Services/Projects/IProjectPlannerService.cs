using Budget.Api.Infrastructure.Storage;
using Budget.Api.Modules.Budget.Contracts.Projects;
using Microsoft.AspNetCore.Http;

namespace Budget.Api.Modules.Budget.Services.Projects;

public interface IProjectPlannerService
{
    Task<ProjectContracts.ProjectsListResponse> GetProjectsAsync(CancellationToken cancellationToken);

    Task<ProjectContracts.ProjectDetailResponse> GetProjectAsync(Guid projectId, CancellationToken cancellationToken);

    Task<ProjectContracts.ProjectDetailResponse> CreateProjectAsync(
        ProjectContracts.CreateProjectRequest request,
        string actor,
        CancellationToken cancellationToken);

    Task<ProjectContracts.ProjectDetailResponse> UpdateProjectAsync(
        Guid projectId,
        ProjectContracts.UpdateProjectRequest request,
        string actor,
        CancellationToken cancellationToken);

    Task ArchiveProjectAsync(Guid projectId, string actor, CancellationToken cancellationToken);

    Task<ProjectContracts.ProjectDetailResponse> AddMilestoneAsync(
        Guid projectId,
        ProjectContracts.CreateMilestoneRequest request,
        string actor,
        CancellationToken cancellationToken);

    Task<ProjectContracts.ProjectDetailResponse> UpdateMilestoneAsync(
        Guid projectId,
        Guid milestoneId,
        ProjectContracts.UpdateMilestoneRequest request,
        string actor,
        CancellationToken cancellationToken);

    Task<ProjectContracts.ProjectDetailResponse> UpdateMilestoneCompletionAsync(
        Guid projectId,
        Guid milestoneId,
        ProjectContracts.UpdateMilestoneCompletionRequest request,
        string actor,
        CancellationToken cancellationToken);

    Task ArchiveMilestoneAsync(Guid projectId, Guid milestoneId, string actor, CancellationToken cancellationToken);

    Task<ProjectContracts.ProjectDetailResponse> AddStepAsync(
        Guid projectId,
        Guid milestoneId,
        ProjectContracts.CreateStepRequest request,
        string actor,
        CancellationToken cancellationToken);

    Task<ProjectContracts.ProjectDetailResponse> UpdateStepAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        ProjectContracts.UpdateStepRequest request,
        string actor,
        CancellationToken cancellationToken);

    Task<ProjectContracts.ProjectDetailResponse> UpdateStepCompletionAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        ProjectContracts.UpdateStepCompletionRequest request,
        string actor,
        CancellationToken cancellationToken);

    Task ArchiveStepAsync(Guid projectId, Guid milestoneId, Guid stepId, string actor, CancellationToken cancellationToken);

    Task<ProjectContracts.ProjectDetailResponse> AddItemAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        ProjectContracts.CreateItemRequest request,
        string actor,
        CancellationToken cancellationToken);

    Task<ProjectContracts.ProjectDetailResponse> UpdateItemAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        Guid itemId,
        ProjectContracts.UpdateItemRequest request,
        string actor,
        CancellationToken cancellationToken);

    Task ArchiveItemAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        Guid itemId,
        string actor,
        CancellationToken cancellationToken);

    Task<ProjectContracts.ProjectDetailResponse> AddPaymentAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        Guid itemId,
        ProjectContracts.CreatePaymentRequest request,
        string actor,
        CancellationToken cancellationToken);

    Task<ProjectContracts.ProjectDetailResponse> UpdatePaymentAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        Guid itemId,
        Guid paymentId,
        ProjectContracts.UpdatePaymentRequest request,
        string actor,
        CancellationToken cancellationToken);

    Task ArchivePaymentAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        Guid itemId,
        Guid paymentId,
        string actor,
        CancellationToken cancellationToken);

    Task<ProjectContracts.ProjectAttachmentResponse> UploadAttachmentAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        Guid itemId,
        ProjectContracts.UploadAttachmentRequest request,
        IFormFile file,
        string actor,
        CancellationToken cancellationToken);

    Task<ProjectContracts.ProjectAttachmentResponse> RemoveAttachmentAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        Guid itemId,
        Guid attachmentId,
        string actor,
        CancellationToken cancellationToken);

    Task<ProjectAttachmentDownloadResult> GetAttachmentDownloadAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        Guid itemId,
        Guid attachmentId,
        CancellationToken cancellationToken);
}
