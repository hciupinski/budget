using Microsoft.Extensions.Options;

namespace Budget.Api.Infrastructure.Storage;

public sealed class ProjectDocumentsStartupValidator(
    IOptions<ProjectDocumentsOptions> options,
    ILogger<ProjectDocumentsStartupValidator> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var rootPath = options.Value.RootPath?.Trim();
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new InvalidOperationException("Project document root path is required. Set ProjectDocuments__RootPath.");
        }

        Directory.CreateDirectory(rootPath);

        var probePath = Path.Combine(rootPath, $".write-probe-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(probePath, "probe");
            File.Delete(probePath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Project document root path '{rootPath}' is not writable.",
                ex);
        }

        logger.LogInformation("Project document storage initialized at {RootPath}", rootPath);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
