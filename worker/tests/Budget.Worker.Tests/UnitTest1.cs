using Budget.Worker.Infrastructure;

namespace Budget.Worker.Tests;

public class WorkerOptionsCustomizationTests
{
    [Fact]
    public void CanSetCustomInterval()
    {
        var options = new WorkerOptions
        {
            IntervalSeconds = 120
        };

        Assert.Equal(120, options.IntervalSeconds);
    }
}
