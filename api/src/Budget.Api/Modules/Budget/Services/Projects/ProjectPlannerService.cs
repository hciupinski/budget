using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Budget.Api.Infrastructure.Persistence;
using Budget.Api.Infrastructure.Storage;
using Budget.Api.Modules.Budget.Contracts.Projects;
using Budget.Api.Modules.Budget.Domain;
using Budget.Api.Modules.Budget.Domain.Projects;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Budget.Api.Modules.Budget.Services.Projects;

public sealed class ProjectPlannerService(
    BudgetDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IMarketPriceService marketPriceService,
    ILogger<ProjectPlannerService> logger,
    IProjectDocumentStorage documentStorage,
    IOptions<ProjectDocumentsOptions> documentOptions)
    : BudgetServiceBase(dbContext, httpClientFactory, marketPriceService, logger),
      IProjectPlannerService
{
    private static readonly HashSet<string> AllowedAttachmentMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly ProjectDocumentsOptions _documentOptions = documentOptions.Value;

    public async Task<ProjectContracts.ProjectsListResponse> GetProjectsAsync(CancellationToken cancellationToken)
    {
        var projects = await dbContext.Projects
            .AsNoTracking()
            .Where(x => !x.IsArchived)
            .Include(x => x.Milestones)
                .ThenInclude(x => x.Steps)
                    .ThenInclude(x => x.Items)
                        .ThenInclude(x => x.Payments)
            .AsSplitQuery()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var summaries = projects.Select(ToProjectSummary).ToArray();
        return new ProjectContracts.ProjectsListResponse(summaries);
    }

    public async Task<ProjectContracts.ProjectDetailResponse> GetProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .Where(x => x.Id == projectId && !x.IsArchived)
            .Include(x => x.Milestones)
                .ThenInclude(x => x.Steps)
                    .ThenInclude(x => x.Items)
                        .ThenInclude(x => x.Payments)
            .Include(x => x.Milestones)
                .ThenInclude(x => x.Steps)
                    .ThenInclude(x => x.Items)
                        .ThenInclude(x => x.Attachments)
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);

        if (project is null)
        {
            throw new KeyNotFoundException("Project was not found.");
        }

        return ToProjectDetail(project);
    }

    public async Task<ProjectContracts.ProjectDetailResponse> CreateProjectAsync(
        ProjectContracts.CreateProjectRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Project name is required.");
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new BudgetProject
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Currency = NormalizeCurrencyCode(request.Currency),
            SortOrder = request.SortOrder,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Projects.Add(entity);
        AddAuditEntry("Project", entity.Id, "PROJECT_CREATED", actor, new
        {
            entity.Name,
            entity.Currency,
            entity.SortOrder
        }, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetProjectAsync(entity.Id, cancellationToken);
    }

    public async Task<ProjectContracts.ProjectDetailResponse> UpdateProjectAsync(
        Guid projectId,
        ProjectContracts.UpdateProjectRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var project = await GetActiveProjectForUpdateAsync(projectId, cancellationToken);

        var changed = false;
        var now = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            project.Name = request.Name.Trim();
            changed = true;
        }

        if (request.Description is not null)
        {
            project.Description = request.Description.Trim();
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(request.Currency))
        {
            project.Currency = NormalizeCurrencyCode(request.Currency);
            changed = true;
        }

        if (request.SortOrder.HasValue)
        {
            project.SortOrder = request.SortOrder.Value;
            changed = true;
        }

        if (changed)
        {
            project.UpdatedAt = now;
            AddAuditEntry("Project", project.Id, "PROJECT_UPDATED", actor, new
            {
                project.Name,
                project.Currency,
                project.SortOrder
            }, now);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await GetProjectAsync(projectId, cancellationToken);
    }

    public async Task ArchiveProjectAsync(Guid projectId, string actor, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .Include(x => x.Milestones)
                .ThenInclude(x => x.Steps)
                    .ThenInclude(x => x.Items)
                        .ThenInclude(x => x.Payments)
            .Include(x => x.Milestones)
                .ThenInclude(x => x.Steps)
                    .ThenInclude(x => x.Items)
                        .ThenInclude(x => x.Attachments)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == projectId && !x.IsArchived, cancellationToken);

        if (project is null)
        {
            throw new KeyNotFoundException("Project was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        await ArchiveProjectGraphAsync(project, actor, now, cancellationToken);
        AddAuditEntry("Project", project.Id, "PROJECT_ARCHIVED", actor, new
        {
            project.Id
        }, now);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProjectContracts.ProjectDetailResponse> AddMilestoneAsync(
        Guid projectId,
        ProjectContracts.CreateMilestoneRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Milestone name is required.");
        }

        _ = await GetActiveProjectForUpdateAsync(projectId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var milestone = new ProjectMilestone
        {
            ProjectId = projectId,
            Name = request.Name.Trim(),
            SortOrder = request.SortOrder,
            CompletionStatus = ProjectCompletionStatus.Active,
            CompletionSource = ProjectCompletionSource.Auto,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.ProjectMilestones.Add(milestone);
        AddAuditEntry("ProjectMilestone", milestone.Id, "MILESTONE_CREATED", actor, new
        {
            milestone.ProjectId,
            milestone.Name,
            milestone.SortOrder
        }, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetProjectAsync(projectId, cancellationToken);
    }

    public async Task<ProjectContracts.ProjectDetailResponse> UpdateMilestoneAsync(
        Guid projectId,
        Guid milestoneId,
        ProjectContracts.UpdateMilestoneRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var milestone = await GetActiveMilestoneForUpdateAsync(projectId, milestoneId, cancellationToken);
        var changed = false;
        var now = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            milestone.Name = request.Name.Trim();
            changed = true;
        }

        if (request.SortOrder.HasValue)
        {
            milestone.SortOrder = request.SortOrder.Value;
            changed = true;
        }

        if (changed)
        {
            milestone.UpdatedAt = now;
            AddAuditEntry("ProjectMilestone", milestone.Id, "MILESTONE_UPDATED", actor, new
            {
                milestone.Name,
                milestone.SortOrder
            }, now);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await GetProjectAsync(projectId, cancellationToken);
    }

    public async Task<ProjectContracts.ProjectDetailResponse> UpdateMilestoneCompletionAsync(
        Guid projectId,
        Guid milestoneId,
        ProjectContracts.UpdateMilestoneCompletionRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var milestone = await dbContext.ProjectMilestones
            .Include(x => x.Steps)
            .SingleOrDefaultAsync(
                x => x.Id == milestoneId &&
                     x.ProjectId == projectId &&
                     !x.IsArchived,
                cancellationToken);

        if (milestone is null)
        {
            throw new KeyNotFoundException("Milestone was not found.");
        }

        var source = ParseCompletionSource(request.Source);
        var now = DateTimeOffset.UtcNow;

        if (source == ProjectCompletionSource.Auto)
        {
            milestone.CompletionSource = ProjectCompletionSource.Auto;
            await RecalculateMilestoneCompletionAsync(milestone.Id, now, cancellationToken);
        }
        else
        {
            var status = ParseCompletionStatus(request.Status);
            milestone.CompletionSource = ProjectCompletionSource.Manual;
            milestone.CompletionStatus = status;
            milestone.CompletedAt = status == ProjectCompletionStatus.Done ? now : null;
            milestone.UpdatedAt = now;
        }

        AddAuditEntry("ProjectMilestone", milestone.Id, "MILESTONE_COMPLETION_UPDATED", actor, new
        {
            source = ToWireValue(milestone.CompletionSource),
            status = ToWireValue(milestone.CompletionStatus)
        }, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetProjectAsync(projectId, cancellationToken);
    }

    public async Task ArchiveMilestoneAsync(Guid projectId, Guid milestoneId, string actor, CancellationToken cancellationToken)
    {
        var milestone = await dbContext.ProjectMilestones
            .Include(x => x.Steps)
                .ThenInclude(x => x.Items)
                    .ThenInclude(x => x.Attachments)
            .AsSplitQuery()
            .SingleOrDefaultAsync(
                x => x.Id == milestoneId &&
                     x.ProjectId == projectId &&
                     !x.IsArchived,
                cancellationToken);

        if (milestone is null)
        {
            throw new KeyNotFoundException("Milestone was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        var attachments = milestone.Steps
            .SelectMany(x => x.Items)
            .SelectMany(x => x.Attachments)
            .ToArray();

        await RelocateAttachmentsToProjectRootAsync(projectId, attachments, cancellationToken);

        dbContext.ProjectMilestones.Remove(milestone);
        AddAuditEntry("ProjectMilestone", milestone.Id, "MILESTONE_DELETED", actor, new
        {
            milestone.Id,
            AttachmentsMoved = attachments.Length
        }, now);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProjectContracts.ProjectDetailResponse> AddStepAsync(
        Guid projectId,
        Guid milestoneId,
        ProjectContracts.CreateStepRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Step name is required.");
        }

        _ = await GetActiveMilestoneForUpdateAsync(projectId, milestoneId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var step = new ProjectStep
        {
            MilestoneId = milestoneId,
            Name = request.Name.Trim(),
            SortOrder = request.SortOrder,
            CompletionStatus = ProjectCompletionStatus.Active,
            CompletionSource = ProjectCompletionSource.Auto,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.ProjectSteps.Add(step);
        AddAuditEntry("ProjectStep", step.Id, "STEP_CREATED", actor, new
        {
            step.MilestoneId,
            step.Name,
            step.SortOrder
        }, now);

        await RecalculateMilestoneCompletionAsync(milestoneId, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetProjectAsync(projectId, cancellationToken);
    }

    public async Task<ProjectContracts.ProjectDetailResponse> UpdateStepAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        ProjectContracts.UpdateStepRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var step = await GetActiveStepForUpdateAsync(projectId, milestoneId, stepId, cancellationToken);
        var changed = false;
        var now = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            step.Name = request.Name.Trim();
            changed = true;
        }

        if (request.SortOrder.HasValue)
        {
            step.SortOrder = request.SortOrder.Value;
            changed = true;
        }

        if (changed)
        {
            step.UpdatedAt = now;
            AddAuditEntry("ProjectStep", step.Id, "STEP_UPDATED", actor, new
            {
                step.Name,
                step.SortOrder
            }, now);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await GetProjectAsync(projectId, cancellationToken);
    }

    public async Task<ProjectContracts.ProjectDetailResponse> UpdateStepCompletionAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        ProjectContracts.UpdateStepCompletionRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var step = await dbContext.ProjectSteps
            .Include(x => x.Items)
            .SingleOrDefaultAsync(
                x => x.Id == stepId &&
                     x.MilestoneId == milestoneId &&
                     !x.IsArchived,
                cancellationToken);

        if (step is null)
        {
            throw new KeyNotFoundException("Step was not found.");
        }

        var milestone = await dbContext.ProjectMilestones
            .SingleOrDefaultAsync(x => x.Id == milestoneId && x.ProjectId == projectId && !x.IsArchived, cancellationToken);
        if (milestone is null)
        {
            throw new KeyNotFoundException("Milestone was not found.");
        }

        var source = ParseCompletionSource(request.Source);
        var now = DateTimeOffset.UtcNow;

        if (source == ProjectCompletionSource.Auto)
        {
            step.CompletionSource = ProjectCompletionSource.Auto;
            await RecalculateStepCompletionAsync(step.Id, now, cancellationToken);
        }
        else
        {
            var status = ParseCompletionStatus(request.Status);
            step.CompletionSource = ProjectCompletionSource.Manual;
            step.CompletionStatus = status;
            step.CompletedAt = status == ProjectCompletionStatus.Done ? now : null;
            step.UpdatedAt = now;
            await RecalculateMilestoneCompletionAsync(milestoneId, now, cancellationToken);
        }

        AddAuditEntry("ProjectStep", step.Id, "STEP_COMPLETION_UPDATED", actor, new
        {
            source = ToWireValue(step.CompletionSource),
            status = ToWireValue(step.CompletionStatus)
        }, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetProjectAsync(projectId, cancellationToken);
    }

    public async Task ArchiveStepAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        string actor,
        CancellationToken cancellationToken)
    {
        var step = await dbContext.ProjectSteps
            .Include(x => x.Items)
                .ThenInclude(x => x.Payments)
            .Include(x => x.Items)
                .ThenInclude(x => x.Attachments)
            .SingleOrDefaultAsync(
                x => x.Id == stepId &&
                     x.MilestoneId == milestoneId &&
                     !x.IsArchived,
                cancellationToken);

        if (step is null)
        {
            throw new KeyNotFoundException("Step was not found.");
        }

        var milestone = await dbContext.ProjectMilestones
            .SingleOrDefaultAsync(x => x.Id == milestoneId && x.ProjectId == projectId && !x.IsArchived, cancellationToken);
        if (milestone is null)
        {
            throw new KeyNotFoundException("Milestone was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        var attachments = step.Items
            .SelectMany(x => x.Attachments)
            .ToArray();

        await RelocateAttachmentsToProjectRootAsync(projectId, attachments, cancellationToken);

        dbContext.ProjectSteps.Remove(step);

        AddAuditEntry("ProjectStep", step.Id, "STEP_DELETED", actor, new
        {
            step.Id,
            AttachmentsMoved = attachments.Length
        }, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await RecalculateMilestoneCompletionAsync(milestoneId, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProjectContracts.ProjectDetailResponse> AddItemAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        ProjectContracts.CreateItemRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Item name is required.");
        }

        var step = await GetActiveStepForUpdateAsync(projectId, milestoneId, stepId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var item = new ProjectCostItem
        {
            StepId = stepId,
            Name = request.Name.Trim(),
            PlannedAmount = RoundAmount(request.PlannedAmount),
            ManualAdjustment = RoundAmount(request.ManualAdjustment),
            IsDone = request.IsDone,
            DoneAt = request.IsDone ? now : null,
            SortOrder = request.SortOrder,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.ProjectItems.Add(item);

        await RecalculateStepCompletionAsync(step.Id, now, cancellationToken);

        AddAuditEntry("ProjectItem", item.Id, "ITEM_CREATED", actor, new
        {
            item.Name,
            item.PlannedAmount,
            item.ManualAdjustment,
            item.IsDone
        }, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetProjectAsync(projectId, cancellationToken);
    }

    public async Task<ProjectContracts.ProjectDetailResponse> UpdateItemAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        Guid itemId,
        ProjectContracts.UpdateItemRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.ProjectItems
            .Include(x => x.Step)
                .ThenInclude(x => x.Milestone)
            .SingleOrDefaultAsync(
                x => x.Id == itemId &&
                     x.StepId == stepId &&
                     !x.IsArchived,
                cancellationToken);

        if (item is null || item.Step.IsArchived || item.Step.Milestone.IsArchived || item.Step.Milestone.ProjectId != projectId || item.Step.MilestoneId != milestoneId)
        {
            throw new KeyNotFoundException("Item was not found.");
        }

        var changed = false;
        var now = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            item.Name = request.Name.Trim();
            changed = true;
        }

        if (request.PlannedAmount.HasValue)
        {
            item.PlannedAmount = RoundAmount(request.PlannedAmount.Value);
            changed = true;
        }

        if (request.ManualAdjustment.HasValue)
        {
            item.ManualAdjustment = RoundAmount(request.ManualAdjustment.Value);
            changed = true;
        }

        if (request.SortOrder.HasValue)
        {
            item.SortOrder = request.SortOrder.Value;
            changed = true;
        }

        if (request.IsDone.HasValue)
        {
            item.IsDone = request.IsDone.Value;
            item.DoneAt = item.IsDone ? now : null;
            changed = true;
        }

        if (changed)
        {
            item.UpdatedAt = now;
            await RecalculateStepCompletionAsync(stepId, now, cancellationToken);
            AddAuditEntry("ProjectItem", item.Id, "ITEM_UPDATED", actor, new
            {
                item.Name,
                item.PlannedAmount,
                item.ManualAdjustment,
                item.IsDone
            }, now);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await GetProjectAsync(projectId, cancellationToken);
    }

    public async Task ArchiveItemAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        Guid itemId,
        string actor,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.ProjectItems
            .Include(x => x.Step)
                .ThenInclude(x => x.Milestone)
            .Include(x => x.Payments)
            .Include(x => x.Attachments)
            .SingleOrDefaultAsync(
                x => x.Id == itemId &&
                     x.StepId == stepId &&
                     !x.IsArchived,
                cancellationToken);

        if (item is null || item.Step.IsArchived || item.Step.Milestone.IsArchived || item.Step.Milestone.ProjectId != projectId || item.Step.MilestoneId != milestoneId)
        {
            throw new KeyNotFoundException("Item was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        await RelocateAttachmentsToProjectRootAsync(projectId, item.Attachments, cancellationToken);
        dbContext.ProjectItems.Remove(item);

        AddAuditEntry("ProjectItem", item.Id, "ITEM_DELETED", actor, new
        {
            item.Id,
            AttachmentsMoved = item.Attachments.Count
        }, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await RecalculateStepCompletionAsync(stepId, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProjectContracts.ProjectDetailResponse> AddPaymentAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        Guid itemId,
        ProjectContracts.CreatePaymentRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            throw new ValidationException("Payment amount must be greater than 0.");
        }

        _ = await GetActiveItemForPaymentMutationsAsync(projectId, milestoneId, stepId, itemId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var payment = new ProjectPayment
        {
            ItemId = itemId,
            Amount = RoundAmount(request.Amount),
            PaymentDate = request.PaymentDate ?? now,
            Note = request.Note?.Trim() ?? string.Empty,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.ProjectPayments.Add(payment);
        AddAuditEntry("ProjectPayment", payment.Id, "PAYMENT_CREATED", actor, new
        {
            payment.Amount,
            payment.PaymentDate,
            payment.Note
        }, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetProjectAsync(projectId, cancellationToken);
    }

    public async Task<ProjectContracts.ProjectDetailResponse> UpdatePaymentAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        Guid itemId,
        Guid paymentId,
        ProjectContracts.UpdatePaymentRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var payment = await dbContext.ProjectPayments
            .Include(x => x.Item)
                .ThenInclude(x => x.Step)
                    .ThenInclude(x => x.Milestone)
            .SingleOrDefaultAsync(
                x => x.Id == paymentId &&
                     x.ItemId == itemId &&
                     !x.IsArchived,
                cancellationToken);

        if (payment is null ||
            payment.Item.IsArchived ||
            payment.Item.Step.IsArchived ||
            payment.Item.Step.Milestone.IsArchived ||
            payment.Item.StepId != stepId ||
            payment.Item.Step.MilestoneId != milestoneId ||
            payment.Item.Step.Milestone.ProjectId != projectId)
        {
            throw new KeyNotFoundException("Payment was not found.");
        }

        var changed = false;
        var now = DateTimeOffset.UtcNow;

        if (request.Amount.HasValue)
        {
            if (request.Amount <= 0)
            {
                throw new ValidationException("Payment amount must be greater than 0.");
            }

            payment.Amount = RoundAmount(request.Amount.Value);
            changed = true;
        }

        if (request.PaymentDate.HasValue)
        {
            payment.PaymentDate = request.PaymentDate.Value;
            changed = true;
        }

        if (request.Note is not null)
        {
            payment.Note = request.Note.Trim();
            changed = true;
        }

        if (changed)
        {
            payment.UpdatedAt = now;
            AddAuditEntry("ProjectPayment", payment.Id, "PAYMENT_UPDATED", actor, new
            {
                payment.Amount,
                payment.PaymentDate,
                payment.Note
            }, now);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await GetProjectAsync(projectId, cancellationToken);
    }

    public async Task ArchivePaymentAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        Guid itemId,
        Guid paymentId,
        string actor,
        CancellationToken cancellationToken)
    {
        var payment = await dbContext.ProjectPayments
            .Include(x => x.Item)
                .ThenInclude(x => x.Step)
                    .ThenInclude(x => x.Milestone)
            .Include(x => x.Attachments)
            .SingleOrDefaultAsync(
                x => x.Id == paymentId &&
                     x.ItemId == itemId &&
                     !x.IsArchived,
                cancellationToken);

        if (payment is null ||
            payment.Item.IsArchived ||
            payment.Item.Step.IsArchived ||
            payment.Item.Step.Milestone.IsArchived ||
            payment.Item.StepId != stepId ||
            payment.Item.Step.MilestoneId != milestoneId ||
            payment.Item.Step.Milestone.ProjectId != projectId)
        {
            throw new KeyNotFoundException("Payment was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        await RelocateAttachmentsToProjectRootAsync(projectId, payment.Attachments, cancellationToken);
        dbContext.ProjectAttachments.RemoveRange(payment.Attachments);
        dbContext.ProjectPayments.Remove(payment);

        AddAuditEntry("ProjectPayment", payment.Id, "PAYMENT_DELETED", actor, new
        {
            payment.Id,
            AttachmentsMoved = payment.Attachments.Count
        }, now);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProjectContracts.ProjectAttachmentResponse> UploadAttachmentAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        Guid itemId,
        ProjectContracts.UploadAttachmentRequest request,
        IFormFile file,
        string actor,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            throw new ValidationException("Attachment file is empty.");
        }

        if (file.Length > _documentOptions.MaxFileSizeBytes)
        {
            throw new ValidationException($"Attachment exceeds max allowed size of {_documentOptions.MaxFileSizeBytes} bytes.");
        }

        if (!AllowedAttachmentMimeTypes.Contains(file.ContentType))
        {
            throw new ValidationException($"Unsupported content type '{file.ContentType}'.");
        }

        var kind = ParseAttachmentKind(request.Kind);

        var item = await dbContext.ProjectItems
            .Include(x => x.Step)
                .ThenInclude(x => x.Milestone)
            .Include(x => x.Payments)
            .Include(x => x.Attachments)
            .SingleOrDefaultAsync(
                x => x.Id == itemId &&
                     x.StepId == stepId &&
                     !x.IsArchived,
                cancellationToken);

        if (item is null || item.Step.IsArchived || item.Step.Milestone.IsArchived || item.Step.Milestone.ProjectId != projectId || item.Step.MilestoneId != milestoneId)
        {
            throw new KeyNotFoundException("Item was not found.");
        }

        if (kind == ProjectAttachmentKind.Agreement && item.Attachments.Any(x => !x.IsRemoved && x.Kind == ProjectAttachmentKind.Agreement))
        {
            throw new ValidationException("Only one active agreement is allowed per item.");
        }

        Guid? paymentId = request.PaymentId;
        if (paymentId.HasValue)
        {
            var paymentExists = item.Payments.Any(x => x.Id == paymentId.Value && !x.IsArchived);
            if (!paymentExists)
            {
                throw new ValidationException("Attachment payment id does not belong to this item.");
            }
        }

        var stored = await documentStorage.SaveAsync(file, projectId, itemId, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var attachment = new ProjectAttachment
        {
            ItemId = itemId,
            PaymentId = paymentId,
            Kind = kind,
            MimeType = file.ContentType,
            SizeBytes = stored.SizeBytes,
            OriginalName = Path.GetFileName(file.FileName),
            StoredRelativePath = stored.RelativePath,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.ProjectAttachments.Add(attachment);

        AddAuditEntry("ProjectAttachment", attachment.Id, "ATTACHMENT_UPLOADED", actor, new
        {
            attachment.ItemId,
            attachment.PaymentId,
            kind = ToWireValue(attachment.Kind),
            attachment.MimeType,
            attachment.SizeBytes,
            attachment.OriginalName
        }, now);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToAttachmentResponse(attachment, projectId, milestoneId, stepId);
    }

    public async Task<ProjectContracts.ProjectAttachmentResponse> RemoveAttachmentAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        Guid itemId,
        Guid attachmentId,
        string actor,
        CancellationToken cancellationToken)
    {
        var attachment = await dbContext.ProjectAttachments
            .Include(x => x.Item)
                .ThenInclude(x => x.Step)
                    .ThenInclude(x => x.Milestone)
            .SingleOrDefaultAsync(
                x => x.Id == attachmentId &&
                     x.ItemId == itemId,
                cancellationToken);

        if (attachment is null ||
            attachment.Item.IsArchived ||
            attachment.Item.Step.IsArchived ||
            attachment.Item.Step.Milestone.IsArchived ||
            attachment.Item.StepId != stepId ||
            attachment.Item.Step.MilestoneId != milestoneId ||
            attachment.Item.Step.Milestone.ProjectId != projectId)
        {
            throw new KeyNotFoundException("Attachment was not found.");
        }

        if (!attachment.IsRemoved)
        {
            var now = DateTimeOffset.UtcNow;
            attachment.StoredRelativePath = await documentStorage.MarkAsDeletedAsync(attachment.StoredRelativePath, cancellationToken);
            attachment.IsRemoved = true;
            attachment.RemovedAt = now;
            attachment.RemovedBy = actor;
            attachment.UpdatedAt = now;

            AddAuditEntry("ProjectAttachment", attachment.Id, "ATTACHMENT_REMOVED", actor, new
            {
                attachment.Id,
                attachment.StoredRelativePath
            }, now);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ToAttachmentResponse(attachment, projectId, milestoneId, stepId);
    }

    public async Task<ProjectAttachmentDownloadResult> GetAttachmentDownloadAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        Guid itemId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var attachment = await dbContext.ProjectAttachments
            .Include(x => x.Item)
                .ThenInclude(x => x.Step)
                    .ThenInclude(x => x.Milestone)
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == attachmentId &&
                     x.ItemId == itemId &&
                     !x.IsRemoved,
                cancellationToken);

        if (attachment is null ||
            attachment.Item.IsArchived ||
            attachment.Item.Step.IsArchived ||
            attachment.Item.Step.Milestone.IsArchived ||
            attachment.Item.StepId != stepId ||
            attachment.Item.Step.MilestoneId != milestoneId ||
            attachment.Item.Step.Milestone.ProjectId != projectId)
        {
            throw new KeyNotFoundException("Attachment was not found.");
        }

        var stream = await documentStorage.OpenReadAsync(attachment.StoredRelativePath, cancellationToken);
        return new ProjectAttachmentDownloadResult
        {
            Stream = stream,
            ContentType = attachment.MimeType,
            FileDownloadName = attachment.OriginalName
        };
    }

    private async Task<BudgetProject> GetActiveProjectForUpdateAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .SingleOrDefaultAsync(x => x.Id == projectId && !x.IsArchived, cancellationToken);

        if (project is null)
        {
            throw new KeyNotFoundException("Project was not found.");
        }

        return project;
    }

    private async Task<ProjectMilestone> GetActiveMilestoneForUpdateAsync(
        Guid projectId,
        Guid milestoneId,
        CancellationToken cancellationToken)
    {
        var milestone = await dbContext.ProjectMilestones
            .SingleOrDefaultAsync(
                x => x.Id == milestoneId &&
                     x.ProjectId == projectId &&
                     !x.IsArchived,
                cancellationToken);

        if (milestone is null)
        {
            throw new KeyNotFoundException("Milestone was not found.");
        }

        return milestone;
    }

    private async Task<ProjectStep> GetActiveStepForUpdateAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        CancellationToken cancellationToken)
    {
        var step = await dbContext.ProjectSteps
            .Include(x => x.Milestone)
            .SingleOrDefaultAsync(
                x => x.Id == stepId &&
                     x.MilestoneId == milestoneId &&
                     !x.IsArchived,
                cancellationToken);

        if (step is null || step.Milestone.IsArchived || step.Milestone.ProjectId != projectId)
        {
            throw new KeyNotFoundException("Step was not found.");
        }

        return step;
    }

    private async Task<ProjectCostItem> GetActiveItemForPaymentMutationsAsync(
        Guid projectId,
        Guid milestoneId,
        Guid stepId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.ProjectItems
            .Include(x => x.Step)
                .ThenInclude(x => x.Milestone)
            .SingleOrDefaultAsync(
                x => x.Id == itemId &&
                     x.StepId == stepId &&
                     !x.IsArchived,
                cancellationToken);

        if (item is null ||
            item.Step.IsArchived ||
            item.Step.Milestone.IsArchived ||
            item.Step.MilestoneId != milestoneId ||
            item.Step.Milestone.ProjectId != projectId)
        {
            throw new KeyNotFoundException("Item was not found.");
        }

        return item;
    }

    private async Task RecalculateStepCompletionAsync(Guid stepId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var step = await dbContext.ProjectSteps
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == stepId, cancellationToken);

        if (step is null || step.IsArchived)
        {
            return;
        }

        if (step.CompletionSource == ProjectCompletionSource.Auto)
        {
            var activeItems = step.Items.Where(x => !x.IsArchived).ToArray();
            var done = activeItems.Length > 0 && activeItems.All(x => x.IsDone);
            step.CompletionStatus = done ? ProjectCompletionStatus.Done : ProjectCompletionStatus.Active;
            step.CompletedAt = done ? now : null;
            step.UpdatedAt = now;
        }

        await RecalculateMilestoneCompletionAsync(step.MilestoneId, now, cancellationToken);
    }

    private async Task RecalculateMilestoneCompletionAsync(Guid milestoneId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var milestone = await dbContext.ProjectMilestones
            .Include(x => x.Steps)
            .SingleOrDefaultAsync(x => x.Id == milestoneId, cancellationToken);

        if (milestone is null || milestone.IsArchived)
        {
            return;
        }

        if (milestone.CompletionSource == ProjectCompletionSource.Auto)
        {
            var activeSteps = milestone.Steps.Where(x => !x.IsArchived).ToArray();
            var done = activeSteps.Length > 0 && activeSteps.All(x => x.CompletionStatus == ProjectCompletionStatus.Done);
            milestone.CompletionStatus = done ? ProjectCompletionStatus.Done : ProjectCompletionStatus.Active;
            milestone.CompletedAt = done ? now : null;
            milestone.UpdatedAt = now;
        }
    }

    private async Task ArchiveProjectGraphAsync(
        BudgetProject project,
        string actor,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!project.IsArchived)
        {
            project.IsArchived = true;
            project.ArchivedAt = now;
            project.ArchivedBy = actor;
            project.UpdatedAt = now;
        }

        foreach (var milestone in project.Milestones.Where(x => !x.IsArchived))
        {
            await ArchiveMilestoneGraphAsync(milestone, actor, now, cancellationToken);
        }
    }

    private async Task ArchiveMilestoneGraphAsync(
        ProjectMilestone milestone,
        string actor,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!milestone.IsArchived)
        {
            milestone.IsArchived = true;
            milestone.ArchivedAt = now;
            milestone.ArchivedBy = actor;
            milestone.UpdatedAt = now;
        }

        foreach (var step in milestone.Steps.Where(x => !x.IsArchived))
        {
            await ArchiveStepGraphAsync(step, actor, now, cancellationToken);
        }
    }

    private async Task ArchiveStepGraphAsync(
        ProjectStep step,
        string actor,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!step.IsArchived)
        {
            step.IsArchived = true;
            step.ArchivedAt = now;
            step.ArchivedBy = actor;
            step.UpdatedAt = now;
        }

        foreach (var item in step.Items.Where(x => !x.IsArchived))
        {
            await ArchiveItemGraphAsync(item, actor, now, cancellationToken);
        }
    }

    private async Task ArchiveItemGraphAsync(
        ProjectCostItem item,
        string actor,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!item.IsArchived)
        {
            item.IsArchived = true;
            item.ArchivedAt = now;
            item.ArchivedBy = actor;
            item.UpdatedAt = now;
        }

        foreach (var payment in item.Payments.Where(x => !x.IsArchived))
        {
            payment.IsArchived = true;
            payment.ArchivedAt = now;
            payment.ArchivedBy = actor;
            payment.UpdatedAt = now;
        }

        foreach (var attachment in item.Attachments.Where(x => !x.IsRemoved))
        {
            attachment.StoredRelativePath = await documentStorage.MarkAsDeletedAsync(attachment.StoredRelativePath, cancellationToken);
            attachment.IsRemoved = true;
            attachment.RemovedAt = now;
            attachment.RemovedBy = actor;
            attachment.UpdatedAt = now;
        }
    }

    private async Task RelocateAttachmentsToProjectRootAsync(
        Guid projectId,
        IEnumerable<ProjectAttachment> attachments,
        CancellationToken cancellationToken)
    {
        foreach (var attachment in attachments)
        {
            if (string.IsNullOrWhiteSpace(attachment.StoredRelativePath))
            {
                continue;
            }

            attachment.StoredRelativePath = await documentStorage.MoveToProjectRootDeletedAsync(
                projectId,
                attachment.StoredRelativePath,
                cancellationToken);
        }
    }

    private void AddAuditEntry(
        string entityType,
        Guid entityId,
        string eventType,
        string actor,
        object payload,
        DateTimeOffset changedAt)
    {
        dbContext.AuditEntries.Add(new AuditEntry
        {
            EntityType = entityType,
            EntityId = entityId,
            EventType = eventType,
            ChangedBy = actor,
            ChangedAt = changedAt,
            Payload = JsonSerializer.Serialize(payload)
        });
    }

    private ProjectContracts.ProjectSummaryResponse ToProjectSummary(BudgetProject project)
    {
        var milestones = project.Milestones.Where(x => !x.IsArchived).ToArray();
        var steps = milestones.SelectMany(x => x.Steps).Where(x => !x.IsArchived).ToArray();
        var items = steps.SelectMany(x => x.Items).Where(x => !x.IsArchived).ToArray();

        var planned = items.Sum(x => x.PlannedAmount);
        var paid = items.Sum(x => x.Payments.Where(p => !p.IsArchived).Sum(p => p.Amount));
        var manualAdjustment = items.Sum(x => x.ManualAdjustment);
        var actual = paid + manualAdjustment;
        var completion = new ProjectContracts.ProjectCompletionSummaryResponse(
            ActiveMilestones: milestones.Count(x => x.CompletionStatus == ProjectCompletionStatus.Active),
            CompletedMilestones: milestones.Count(x => x.CompletionStatus == ProjectCompletionStatus.Done),
            ActiveSteps: steps.Count(x => x.CompletionStatus == ProjectCompletionStatus.Active),
            CompletedSteps: steps.Count(x => x.CompletionStatus == ProjectCompletionStatus.Done),
            OpenItems: items.Count(x => !x.IsDone),
            DoneItems: items.Count(x => x.IsDone));

        return new ProjectContracts.ProjectSummaryResponse(
            project.Id,
            project.Name,
            project.Currency,
            project.IsArchived,
            new ProjectContracts.ProjectTotalsResponse(
                Planned: planned,
                Paid: paid,
                ManualAdjustment: manualAdjustment,
                Actual: actual,
                Variance: actual - planned),
            completion,
            project.UpdatedAt);
    }

    private ProjectContracts.ProjectDetailResponse ToProjectDetail(BudgetProject project)
    {
        var milestones = project.Milestones
            .Where(x => !x.IsArchived)
            .Select(x => ToMilestoneResponse(project.Id, x))
            .OrderBy(x => x.CompletionStatus == "DONE")
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var allItems = milestones
            .SelectMany(x => x.Steps)
            .SelectMany(x => x.Items)
            .ToArray();

        var totals = BuildTotals(allItems);
        var completion = new ProjectContracts.ProjectCompletionSummaryResponse(
            ActiveMilestones: milestones.Count(x => x.CompletionStatus == "ACTIVE"),
            CompletedMilestones: milestones.Count(x => x.CompletionStatus == "DONE"),
            ActiveSteps: milestones.SelectMany(x => x.Steps).Count(x => x.CompletionStatus == "ACTIVE"),
            CompletedSteps: milestones.SelectMany(x => x.Steps).Count(x => x.CompletionStatus == "DONE"),
            OpenItems: allItems.Count(x => !x.IsDone),
            DoneItems: allItems.Count(x => x.IsDone));

        return new ProjectContracts.ProjectDetailResponse(
            project.Id,
            project.Name,
            project.Description,
            project.Currency,
            project.SortOrder,
            project.IsArchived,
            project.ArchivedAt,
            project.CreatedAt,
            project.UpdatedAt,
            totals,
            completion,
            milestones);
    }

    private ProjectContracts.ProjectMilestoneResponse ToMilestoneResponse(Guid projectId, ProjectMilestone milestone)
    {
        var steps = milestone.Steps
            .Where(x => !x.IsArchived)
            .Select(x => ToStepResponse(projectId, milestone.Id, x))
            .OrderBy(x => x.CompletionStatus == "DONE")
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var items = steps.SelectMany(x => x.Items).ToArray();
        var completion = new ProjectContracts.ProjectCompletionSummaryResponse(
            ActiveMilestones: milestone.CompletionStatus == ProjectCompletionStatus.Active ? 1 : 0,
            CompletedMilestones: milestone.CompletionStatus == ProjectCompletionStatus.Done ? 1 : 0,
            ActiveSteps: steps.Count(x => x.CompletionStatus == "ACTIVE"),
            CompletedSteps: steps.Count(x => x.CompletionStatus == "DONE"),
            OpenItems: items.Count(x => !x.IsDone),
            DoneItems: items.Count(x => x.IsDone));

        return new ProjectContracts.ProjectMilestoneResponse(
            milestone.Id,
            milestone.Name,
            milestone.SortOrder,
            ToWireValue(milestone.CompletionStatus),
            ToWireValue(milestone.CompletionSource),
            milestone.CompletedAt,
            BuildTotals(items),
            completion,
            steps);
    }

    private ProjectContracts.ProjectStepResponse ToStepResponse(Guid projectId, Guid milestoneId, ProjectStep step)
    {
        var items = step.Items
            .Where(x => !x.IsArchived)
            .Select(x => ToItemResponse(projectId, milestoneId, step.Id, x))
            .OrderBy(x => x.IsDone)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var completion = new ProjectContracts.ProjectCompletionSummaryResponse(
            ActiveMilestones: 0,
            CompletedMilestones: 0,
            ActiveSteps: step.CompletionStatus == ProjectCompletionStatus.Active ? 1 : 0,
            CompletedSteps: step.CompletionStatus == ProjectCompletionStatus.Done ? 1 : 0,
            OpenItems: items.Count(x => !x.IsDone),
            DoneItems: items.Count(x => x.IsDone));

        return new ProjectContracts.ProjectStepResponse(
            step.Id,
            step.Name,
            step.SortOrder,
            ToWireValue(step.CompletionStatus),
            ToWireValue(step.CompletionSource),
            step.CompletedAt,
            BuildTotals(items),
            completion,
            items);
    }

    private ProjectContracts.ProjectItemResponse ToItemResponse(Guid projectId, Guid milestoneId, Guid stepId, ProjectCostItem item)
    {
        var payments = item.Payments
            .Where(x => !x.IsArchived)
            .OrderByDescending(x => x.PaymentDate)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new ProjectContracts.ProjectPaymentResponse(
                x.Id,
                x.Amount,
                x.PaymentDate,
                x.Note,
                x.IsArchived,
                x.UpdatedAt))
            .ToArray();

        var attachments = item.Attachments
            .Where(x => !x.IsRemoved)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => ToAttachmentResponse(x, projectId, milestoneId, stepId))
            .ToArray();

        var paid = payments.Sum(x => x.Amount);
        var actual = paid + item.ManualAdjustment;

        return new ProjectContracts.ProjectItemResponse(
            item.Id,
            item.Name,
            item.SortOrder,
            item.PlannedAmount,
            paid,
            item.ManualAdjustment,
            actual,
            actual - item.PlannedAmount,
            item.IsDone,
            item.DoneAt,
            payments,
            attachments);
    }

    private static ProjectContracts.ProjectTotalsResponse BuildTotals(IEnumerable<ProjectContracts.ProjectItemResponse> items)
    {
        var array = items as ProjectContracts.ProjectItemResponse[] ?? items.ToArray();
        var planned = array.Sum(x => x.PlannedAmount);
        var paid = array.Sum(x => x.PaidAmount);
        var manual = array.Sum(x => x.ManualAdjustment);
        var actual = paid + manual;

        return new ProjectContracts.ProjectTotalsResponse(
            Planned: planned,
            Paid: paid,
            ManualAdjustment: manual,
            Actual: actual,
            Variance: actual - planned);
    }

    private ProjectContracts.ProjectAttachmentResponse ToAttachmentResponse(
        ProjectAttachment attachment,
        Guid projectId,
        Guid milestoneId,
        Guid stepId)
    {
        return new ProjectContracts.ProjectAttachmentResponse(
            attachment.Id,
            attachment.ItemId,
            attachment.PaymentId,
            ToWireValue(attachment.Kind),
            attachment.MimeType,
            attachment.SizeBytes,
            attachment.OriginalName,
            attachment.CreatedAt,
            attachment.IsRemoved,
            attachment.RemovedAt,
            $"/api/budget/projects/{projectId}/milestones/{milestoneId}/steps/{stepId}/items/{attachment.ItemId}/attachments/{attachment.Id}/download");
    }

    private static decimal RoundAmount(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static ProjectCompletionStatus ParseCompletionStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("Completion status is required for manual updates.");
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "ACTIVE" => ProjectCompletionStatus.Active,
            "DONE" => ProjectCompletionStatus.Done,
            _ => throw new ValidationException($"Unsupported completion status '{value}'.")
        };
    }

    private static ProjectCompletionSource ParseCompletionSource(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("Completion source is required.");
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "AUTO" => ProjectCompletionSource.Auto,
            "MANUAL" => ProjectCompletionSource.Manual,
            _ => throw new ValidationException($"Unsupported completion source '{value}'.")
        };
    }

    private static ProjectAttachmentKind ParseAttachmentKind(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("Attachment kind is required.");
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "AGREEMENT" => ProjectAttachmentKind.Agreement,
            "RECEIPT" => ProjectAttachmentKind.Receipt,
            "DOCUMENT" => ProjectAttachmentKind.Document,
            _ => throw new ValidationException($"Unsupported attachment kind '{value}'.")
        };
    }

    private static string ToWireValue(ProjectCompletionStatus status)
    {
        return status == ProjectCompletionStatus.Done ? "DONE" : "ACTIVE";
    }

    private static string ToWireValue(ProjectCompletionSource source)
    {
        return source == ProjectCompletionSource.Manual ? "MANUAL" : "AUTO";
    }

    private static string ToWireValue(ProjectAttachmentKind kind)
    {
        return kind switch
        {
            ProjectAttachmentKind.Agreement => "AGREEMENT",
            ProjectAttachmentKind.Receipt => "RECEIPT",
            _ => "DOCUMENT"
        };
    }
}
