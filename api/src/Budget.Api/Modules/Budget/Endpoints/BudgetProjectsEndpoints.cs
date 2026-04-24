using System.Security.Claims;
using Budget.Api.Modules.Budget.Contracts.Projects;
using Budget.Api.Modules.Budget.Services.Projects;

namespace Budget.Api.Modules.Budget.Endpoints;

public static class BudgetProjectsEndpoints
{
    public static RouteGroupBuilder MapBudgetProjectsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/projects", async (
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            var result = await service.GetProjectsAsync(ct);
            return Results.Ok(result);
        });

        group.MapPost("/projects", async (
            ProjectContracts.CreateProjectRequest request,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            var result = await service.CreateProjectAsync(request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapGet("/projects/{projectId:guid}", async (
            Guid projectId,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            var result = await service.GetProjectAsync(projectId, ct);
            return Results.Ok(result);
        });

        group.MapPatch("/projects/{projectId:guid}", async (
            Guid projectId,
            ProjectContracts.UpdateProjectRequest request,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdateProjectAsync(projectId, request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapPost("/projects/{projectId:guid}/archive", async (
            Guid projectId,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            await service.ArchiveProjectAsync(projectId, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.NoContent();
        });

        group.MapPost("/projects/{projectId:guid}/milestones", async (
            Guid projectId,
            ProjectContracts.CreateMilestoneRequest request,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            var result = await service.AddMilestoneAsync(projectId, request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapPatch("/projects/{projectId:guid}/milestones/{milestoneId:guid}", async (
            Guid projectId,
            Guid milestoneId,
            ProjectContracts.UpdateMilestoneRequest request,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdateMilestoneAsync(projectId, milestoneId, request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapPatch("/projects/{projectId:guid}/milestones/{milestoneId:guid}/completion", async (
            Guid projectId,
            Guid milestoneId,
            ProjectContracts.UpdateMilestoneCompletionRequest request,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdateMilestoneCompletionAsync(projectId, milestoneId, request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapPost("/projects/{projectId:guid}/milestones/{milestoneId:guid}/archive", async (
            Guid projectId,
            Guid milestoneId,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            await service.ArchiveMilestoneAsync(projectId, milestoneId, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.NoContent();
        });

        group.MapDelete("/projects/{projectId:guid}/milestones/{milestoneId:guid}", async (
            Guid projectId,
            Guid milestoneId,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            await service.ArchiveMilestoneAsync(projectId, milestoneId, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.NoContent();
        });

        group.MapPost("/projects/{projectId:guid}/milestones/{milestoneId:guid}/steps", async (
            Guid projectId,
            Guid milestoneId,
            ProjectContracts.CreateStepRequest request,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            var result = await service.AddStepAsync(projectId, milestoneId, request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapPatch("/projects/{projectId:guid}/milestones/{milestoneId:guid}/steps/{stepId:guid}", async (
            Guid projectId,
            Guid milestoneId,
            Guid stepId,
            ProjectContracts.UpdateStepRequest request,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdateStepAsync(projectId, milestoneId, stepId, request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapPatch("/projects/{projectId:guid}/milestones/{milestoneId:guid}/steps/{stepId:guid}/completion", async (
            Guid projectId,
            Guid milestoneId,
            Guid stepId,
            ProjectContracts.UpdateStepCompletionRequest request,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdateStepCompletionAsync(projectId, milestoneId, stepId, request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapPost("/projects/{projectId:guid}/milestones/{milestoneId:guid}/steps/{stepId:guid}/archive", async (
            Guid projectId,
            Guid milestoneId,
            Guid stepId,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            await service.ArchiveStepAsync(projectId, milestoneId, stepId, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.NoContent();
        });

        group.MapDelete("/projects/{projectId:guid}/milestones/{milestoneId:guid}/steps/{stepId:guid}", async (
            Guid projectId,
            Guid milestoneId,
            Guid stepId,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            await service.ArchiveStepAsync(projectId, milestoneId, stepId, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.NoContent();
        });

        group.MapPost("/projects/{projectId:guid}/milestones/{milestoneId:guid}/steps/{stepId:guid}/items", async (
            Guid projectId,
            Guid milestoneId,
            Guid stepId,
            ProjectContracts.CreateItemRequest request,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            var result = await service.AddItemAsync(projectId, milestoneId, stepId, request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapPatch("/projects/{projectId:guid}/milestones/{milestoneId:guid}/steps/{stepId:guid}/items/{itemId:guid}", async (
            Guid projectId,
            Guid milestoneId,
            Guid stepId,
            Guid itemId,
            ProjectContracts.UpdateItemRequest request,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdateItemAsync(projectId, milestoneId, stepId, itemId, request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapPost("/projects/{projectId:guid}/milestones/{milestoneId:guid}/steps/{stepId:guid}/items/{itemId:guid}/archive", async (
            Guid projectId,
            Guid milestoneId,
            Guid stepId,
            Guid itemId,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            await service.ArchiveItemAsync(projectId, milestoneId, stepId, itemId, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.NoContent();
        });

        group.MapDelete("/projects/{projectId:guid}/milestones/{milestoneId:guid}/steps/{stepId:guid}/items/{itemId:guid}", async (
            Guid projectId,
            Guid milestoneId,
            Guid stepId,
            Guid itemId,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            await service.ArchiveItemAsync(projectId, milestoneId, stepId, itemId, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.NoContent();
        });

        group.MapPost("/projects/{projectId:guid}/milestones/{milestoneId:guid}/steps/{stepId:guid}/items/{itemId:guid}/payments", async (
            Guid projectId,
            Guid milestoneId,
            Guid stepId,
            Guid itemId,
            ProjectContracts.CreatePaymentRequest request,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            var result = await service.AddPaymentAsync(projectId, milestoneId, stepId, itemId, request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapPatch("/projects/{projectId:guid}/milestones/{milestoneId:guid}/steps/{stepId:guid}/items/{itemId:guid}/payments/{paymentId:guid}", async (
            Guid projectId,
            Guid milestoneId,
            Guid stepId,
            Guid itemId,
            Guid paymentId,
            ProjectContracts.UpdatePaymentRequest request,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdatePaymentAsync(projectId, milestoneId, stepId, itemId, paymentId, request, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapPost("/projects/{projectId:guid}/milestones/{milestoneId:guid}/steps/{stepId:guid}/items/{itemId:guid}/payments/{paymentId:guid}/archive", async (
            Guid projectId,
            Guid milestoneId,
            Guid stepId,
            Guid itemId,
            Guid paymentId,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            await service.ArchivePaymentAsync(projectId, milestoneId, stepId, itemId, paymentId, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.NoContent();
        });

        group.MapDelete("/projects/{projectId:guid}/milestones/{milestoneId:guid}/steps/{stepId:guid}/items/{itemId:guid}/payments/{paymentId:guid}", async (
            Guid projectId,
            Guid milestoneId,
            Guid stepId,
            Guid itemId,
            Guid paymentId,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            await service.ArchivePaymentAsync(projectId, milestoneId, stepId, itemId, paymentId, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.NoContent();
        });

        group.MapPost("/projects/{projectId:guid}/milestones/{milestoneId:guid}/steps/{stepId:guid}/items/{itemId:guid}/attachments", async (
            Guid projectId,
            Guid milestoneId,
            Guid stepId,
            Guid itemId,
            HttpRequest request,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var kind = form["kind"].ToString();
            var paymentIdRaw = form["paymentId"].ToString();
            Guid? paymentId = null;
            if (!string.IsNullOrWhiteSpace(paymentIdRaw) && Guid.TryParse(paymentIdRaw, out var parsedPaymentId))
            {
                paymentId = parsedPaymentId;
            }

            var file = form.Files.GetFile("file");
            if (file is null)
            {
                return Results.BadRequest(new { error = "Missing file form field 'file'." });
            }

            var payload = new ProjectContracts.UploadAttachmentRequest(kind, paymentId);
            var result = await service.UploadAttachmentAsync(
                projectId,
                milestoneId,
                stepId,
                itemId,
                payload,
                file,
                BudgetEndpointUser.CurrentUser(user),
                ct);

            return Results.Ok(result);
        }).DisableAntiforgery();

        group.MapPost("/projects/{projectId:guid}/milestones/{milestoneId:guid}/steps/{stepId:guid}/items/{itemId:guid}/attachments/{attachmentId:guid}/remove", async (
            Guid projectId,
            Guid milestoneId,
            Guid stepId,
            Guid itemId,
            Guid attachmentId,
            ClaimsPrincipal user,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            var result = await service.RemoveAttachmentAsync(projectId, milestoneId, stepId, itemId, attachmentId, BudgetEndpointUser.CurrentUser(user), ct);
            return Results.Ok(result);
        });

        group.MapGet("/projects/{projectId:guid}/milestones/{milestoneId:guid}/steps/{stepId:guid}/items/{itemId:guid}/attachments/{attachmentId:guid}/download", async (
            Guid projectId,
            Guid milestoneId,
            Guid stepId,
            Guid itemId,
            Guid attachmentId,
            IProjectPlannerService service,
            CancellationToken ct) =>
        {
            var result = await service.GetAttachmentDownloadAsync(projectId, milestoneId, stepId, itemId, attachmentId, ct);
            return Results.File(result.Stream, result.ContentType, result.FileDownloadName);
        });

        return group;
    }
}
