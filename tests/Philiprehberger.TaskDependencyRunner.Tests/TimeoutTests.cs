using Xunit;
namespace Philiprehberger.TaskDependencyRunner.Tests;

public class TimeoutTests
{
    [Fact]
    public async Task TaskWithTimeout_ThrowsTaskTimeoutException_WhenExceeded()
    {
        var graph = new TaskGraph()
            .Add("slow", async () => await Task.Delay(5000), timeout: TimeSpan.FromMilliseconds(50));

        var ex = await Assert.ThrowsAsync<TaskTimeoutException>(() => graph.RunAsync());
        Assert.Equal("slow", ex.TaskName);
    }

    [Fact]
    public async Task TaskWithTimeout_CompletesNormally_WhenWithinLimit()
    {
        var completed = false;

        var graph = new TaskGraph()
            .Add("fast", () => { completed = true; }, timeout: TimeSpan.FromSeconds(5));

        await graph.RunAsync();

        Assert.True(completed);
    }

    [Fact]
    public async Task TaskTimeoutException_HasCorrectMessage()
    {
        var graph = new TaskGraph()
            .Add("worker", async () => await Task.Delay(5000), timeout: TimeSpan.FromMilliseconds(50));

        var ex = await Assert.ThrowsAsync<TaskTimeoutException>(() => graph.RunAsync());
        Assert.Contains("worker", ex.Message);
    }
}
