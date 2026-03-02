using Budget.Worker.Infrastructure;
using Microsoft.Extensions.Options;

namespace Budget.Worker;

public sealed class ScheduledWorker(
    ILogger<ScheduledWorker> logger,
    IOptions<WorkerOptions> options) : BackgroundService
{
    private readonly WorkerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Budget worker started with {IntervalSeconds}s interval", _options.IntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Heartbeat at {UtcNow}", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromSeconds(_options.IntervalSeconds), stoppingToken);
        }

        logger.LogInformation("Budget worker stopped");
    }
}
