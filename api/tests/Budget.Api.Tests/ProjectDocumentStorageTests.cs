using Budget.Api.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Budget.Api.Tests;

public sealed class ProjectDocumentStorageTests
{
    [Fact]
    public async Task MarkAsDeletedAsync_PrefixesFileNameAndIsIdempotent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"budget-storage-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        var options = Options.Create(new ProjectDocumentsOptions
        {
            RootPath = root,
            MaxFileSizeBytes = 26214400
        });

        var storage = new ProjectDocumentStorage(options, NullLogger<ProjectDocumentStorage>.Instance);

        var relative = "abc/file.pdf";
        var fullDir = Path.Combine(root, "abc");
        Directory.CreateDirectory(fullDir);
        var fullPath = Path.Combine(fullDir, "file.pdf");
        await File.WriteAllTextAsync(fullPath, "test");

        var first = await storage.MarkAsDeletedAsync(relative, CancellationToken.None);
        var second = await storage.MarkAsDeletedAsync(first, CancellationToken.None);

        Assert.StartsWith("xdel_", Path.GetFileName(first));
        Assert.Equal(first, second);
        Assert.True(File.Exists(Path.Combine(root, first.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public async Task OpenReadAsync_RejectsTraversalPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), $"budget-storage-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        var options = Options.Create(new ProjectDocumentsOptions
        {
            RootPath = root,
            MaxFileSizeBytes = 26214400
        });

        var storage = new ProjectDocumentStorage(options, NullLogger<ProjectDocumentStorage>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.OpenReadAsync("../outside.pdf", CancellationToken.None));
    }

    [Fact]
    public async Task MoveToProjectRootDeletedAsync_MovesAndPrefixesFileName()
    {
        var root = Path.Combine(Path.GetTempPath(), $"budget-storage-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        var options = Options.Create(new ProjectDocumentsOptions
        {
            RootPath = root,
            MaxFileSizeBytes = 26214400
        });

        var storage = new ProjectDocumentStorage(options, NullLogger<ProjectDocumentStorage>.Instance);
        var projectId = Guid.NewGuid();
        var relative = $"nested/{Guid.NewGuid():N}/file.pdf";
        var sourcePath = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        await File.WriteAllTextAsync(sourcePath, "test");

        var moved = await storage.MoveToProjectRootDeletedAsync(projectId, relative, CancellationToken.None);
        var movedPath = Path.Combine(root, moved.Replace('/', Path.DirectorySeparatorChar));

        Assert.StartsWith($"{projectId:N}/xdel_", moved, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(sourcePath));
        Assert.True(File.Exists(movedPath));
    }
}
