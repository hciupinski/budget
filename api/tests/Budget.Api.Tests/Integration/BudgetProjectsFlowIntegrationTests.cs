using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Budget.Api.Infrastructure.Persistence;
using Budget.Api.Infrastructure.Storage;
using Budget.Api.Modules.Budget.Contracts.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Budget.Api.Tests.Integration;

public sealed class BudgetProjectsFlowIntegrationTests
{
    [Fact]
    public async Task ProjectEndpoints_EndToEndFlow_WorksWithAttachmentSoftRemove()
    {
        await using var host = await BudgetApiIntegrationTestHost.StartAsync();
        var client = await host.CreateAuthenticatedClientAsync();

        var createdProject = await (await client.PostAsJsonAsync(
            "/api/budget/projects",
            new ProjectContracts.CreateProjectRequest(
                Name: "House Build",
                Description: "Primary house construction",
                Currency: "USD",
                SortOrder: 10)))
            .Content
            .ReadFromJsonAsync<ProjectContracts.ProjectDetailResponse>();

        Assert.NotNull(createdProject);
        var projectId = createdProject.Id;

        var afterMilestone = await (await client.PostAsJsonAsync(
            $"/api/budget/projects/{projectId}/milestones",
            new ProjectContracts.CreateMilestoneRequest("Fundaments", 10)))
            .Content
            .ReadFromJsonAsync<ProjectContracts.ProjectDetailResponse>();

        Assert.NotNull(afterMilestone);
        var milestoneId = afterMilestone.Milestones.Single().Id;

        var afterStep = await (await client.PostAsJsonAsync(
            $"/api/budget/projects/{projectId}/milestones/{milestoneId}/steps",
            new ProjectContracts.CreateStepRequest("Flatten area", 10)))
            .Content
            .ReadFromJsonAsync<ProjectContracts.ProjectDetailResponse>();

        Assert.NotNull(afterStep);
        var stepId = afterStep.Milestones.Single().Steps.Single().Id;

        var afterItem = await (await client.PostAsJsonAsync(
            $"/api/budget/projects/{projectId}/milestones/{milestoneId}/steps/{stepId}/items",
            new ProjectContracts.CreateItemRequest(
                Name: "Rent machine",
                PlannedAmount: 200m,
                ManualAdjustment: 10m,
                IsDone: false,
                SortOrder: 10)))
            .Content
            .ReadFromJsonAsync<ProjectContracts.ProjectDetailResponse>();

        Assert.NotNull(afterItem);
        var item = afterItem.Milestones.Single().Steps.Single().Items.Single();
        var itemId = item.Id;

        var afterPayment = await (await client.PostAsJsonAsync(
            $"/api/budget/projects/{projectId}/milestones/{milestoneId}/steps/{stepId}/items/{itemId}/payments",
            new ProjectContracts.CreatePaymentRequest(
                Amount: 150m,
                PaymentDate: DateTimeOffset.UtcNow,
                Note: "First invoice")))
            .Content
            .ReadFromJsonAsync<ProjectContracts.ProjectDetailResponse>();

        Assert.NotNull(afterPayment);
        var itemAfterPayment = afterPayment.Milestones.Single().Steps.Single().Items.Single();
        Assert.Equal(150m, itemAfterPayment.PaidAmount);
        Assert.Equal(160m, itemAfterPayment.ActualAmount);

        using var uploadContent = new MultipartFormDataContent();
        uploadContent.Add(new StringContent("AGREEMENT"), "kind");
        var fileContent = new ByteArrayContent("%PDF-1.4 fake"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        uploadContent.Add(fileContent, "file", "agreement.pdf");

        var uploadResponse = await client.PostAsync(
            $"/api/budget/projects/{projectId}/milestones/{milestoneId}/steps/{stepId}/items/{itemId}/attachments",
            uploadContent);

        uploadResponse.EnsureSuccessStatusCode();
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<ProjectContracts.ProjectAttachmentResponse>();
        Assert.NotNull(uploaded);
        Assert.Equal("AGREEMENT", uploaded.Kind);

        using var duplicateContent = new MultipartFormDataContent();
        duplicateContent.Add(new StringContent("AGREEMENT"), "kind");
        var dupFileContent = new ByteArrayContent("%PDF-1.4 fake2"u8.ToArray());
        dupFileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        duplicateContent.Add(dupFileContent, "file", "agreement-2.pdf");

        var duplicateResponse = await client.PostAsync(
            $"/api/budget/projects/{projectId}/milestones/{milestoneId}/steps/{stepId}/items/{itemId}/attachments",
            duplicateContent);

        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);

        var removeResponse = await client.PostAsync(
            $"/api/budget/projects/{projectId}/milestones/{milestoneId}/steps/{stepId}/items/{itemId}/attachments/{uploaded.Id}/remove",
            null);

        removeResponse.EnsureSuccessStatusCode();
        var removed = await removeResponse.Content.ReadFromJsonAsync<ProjectContracts.ProjectAttachmentResponse>();
        Assert.NotNull(removed);
        Assert.True(removed.IsRemoved);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ProjectDocumentsOptions>>();
        var attachmentEntity = await db.ProjectAttachments.SingleAsync(x => x.Id == uploaded.Id);

        Assert.True(attachmentEntity.IsRemoved);
        Assert.StartsWith("xdel_", Path.GetFileName(attachmentEntity.StoredRelativePath));

        var absolutePath = Path.Combine(options.Value.RootPath, attachmentEntity.StoredRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(absolutePath));
    }

    [Fact]
    public async Task DeleteItem_HardDeletesMetadata_AndMovesFilesToProjectRoot()
    {
        await using var host = await BudgetApiIntegrationTestHost.StartAsync();
        var client = await host.CreateAuthenticatedClientAsync();

        var createdProject = await (await client.PostAsJsonAsync(
            "/api/budget/projects",
            new ProjectContracts.CreateProjectRequest(
                Name: "House Build",
                Description: "Delete flow",
                Currency: "USD",
                SortOrder: 10)))
            .Content
            .ReadFromJsonAsync<ProjectContracts.ProjectDetailResponse>();

        Assert.NotNull(createdProject);
        var projectId = createdProject.Id;

        var afterMilestone = await (await client.PostAsJsonAsync(
            $"/api/budget/projects/{projectId}/milestones",
            new ProjectContracts.CreateMilestoneRequest("Fundaments", 10)))
            .Content
            .ReadFromJsonAsync<ProjectContracts.ProjectDetailResponse>();

        Assert.NotNull(afterMilestone);
        var milestoneId = afterMilestone.Milestones.Single().Id;

        var afterStep = await (await client.PostAsJsonAsync(
            $"/api/budget/projects/{projectId}/milestones/{milestoneId}/steps",
            new ProjectContracts.CreateStepRequest("Dig hole", 10)))
            .Content
            .ReadFromJsonAsync<ProjectContracts.ProjectDetailResponse>();

        Assert.NotNull(afterStep);
        var stepId = afterStep.Milestones.Single().Steps.Single().Id;

        var afterItem = await (await client.PostAsJsonAsync(
            $"/api/budget/projects/{projectId}/milestones/{milestoneId}/steps/{stepId}/items",
            new ProjectContracts.CreateItemRequest(
                Name: "Rent machine",
                PlannedAmount: 200m,
                ManualAdjustment: 0m,
                IsDone: false,
                SortOrder: 10)))
            .Content
            .ReadFromJsonAsync<ProjectContracts.ProjectDetailResponse>();

        Assert.NotNull(afterItem);
        var itemId = afterItem.Milestones.Single().Steps.Single().Items.Single().Id;

        using var uploadContent = new MultipartFormDataContent();
        uploadContent.Add(new StringContent("DOCUMENT"), "kind");
        var fileContent = new ByteArrayContent("%PDF-1.4 fake-delete"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        uploadContent.Add(fileContent, "file", "contract.pdf");

        var uploadResponse = await client.PostAsync(
            $"/api/budget/projects/{projectId}/milestones/{milestoneId}/steps/{stepId}/items/{itemId}/attachments",
            uploadContent);

        uploadResponse.EnsureSuccessStatusCode();
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<ProjectContracts.ProjectAttachmentResponse>();
        Assert.NotNull(uploaded);

        var deleteResponse = await client.DeleteAsync(
            $"/api/budget/projects/{projectId}/milestones/{milestoneId}/steps/{stepId}/items/{itemId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ProjectDocumentsOptions>>();

        var deletedItem = await db.ProjectItems.SingleOrDefaultAsync(x => x.Id == itemId);
        var deletedAttachment = await db.ProjectAttachments.SingleOrDefaultAsync(x => x.Id == uploaded.Id);
        Assert.Null(deletedItem);
        Assert.Null(deletedAttachment);

        var projectRoot = Path.Combine(options.Value.RootPath, projectId.ToString("N"));
        Assert.True(Directory.Exists(projectRoot));
        Assert.NotEmpty(Directory.GetFiles(projectRoot, "xdel_*", SearchOption.TopDirectoryOnly));
    }
}
