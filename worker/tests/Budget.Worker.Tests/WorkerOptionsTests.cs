using Budget.Worker.Infrastructure;

namespace Budget.Worker.Tests;

public class WorkerOptionsTests
{
    [Fact]
    public void DefaultInterval_IsSixtySeconds()
    {
        var options = new WorkerOptions();

        Assert.Equal(60, options.IntervalSeconds);
    }
}
