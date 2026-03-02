using System.ComponentModel.DataAnnotations;

namespace Budget.Worker.Infrastructure;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    [Range(10, 3600)]
    public int IntervalSeconds { get; init; } = 60;
}
