using Xunit;
namespace Philiprehberger.TaskDependencyRunner.Tests;

public class ProgressReporterTests
{
    private sealed class TestReporter : IProgressReporter
    {
        public List<string> Started { get; } = new();
        public List<(string Name, TimeSpan Elapsed)> Completed { get; } = new();
        public List<(string Name, Exception Ex)> Failed { get; } = new();

        public void OnTaskStarted(string name) => Started.Add(name);
        public void OnTaskCompleted(string name, TimeSpan elapsed) => Completed.Add((name, elapsed));
        public void OnTaskFailed(string name, Exception exception) => Failed.Add((name, exception));
    }

    [Fact]
    public async Task ProgressReporter_ReceivesStartAndCompleteEvents()
    {
        var reporter = new TestReporter();
        var graph = new TaskGraph { ProgressReporter = reporter };
        graph.Add("a", () => { }).Add("b", () => { }, "a");

        await graph.RunAsync();

        Assert.Equal(2, reporter.Started.Count);
        Assert.Equal(2, reporter.Completed.Count);
        Assert.Contains("a", reporter.Started);
        Assert.Contains("b", reporter.Started);
    }

    [Fact]
    public async Task ProgressReporter_ReceivesFailEvent_OnTimeout()
    {
        var reporter = new TestReporter();
        var graph = new TaskGraph { ProgressReporter = reporter };
        graph.Add("slow", async () => await Task.Delay(5000), timeout: TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<TaskTimeoutException>(() => graph.RunAsync());

        Assert.Single(reporter.Failed);
        Assert.Equal("slow", reporter.Failed[0].Name);
        Assert.IsType<TaskTimeoutException>(reporter.Failed[0].Ex);
    }

    [Fact]
    public async Task ProgressReporter_ElapsedIsPositive()
    {
        var reporter = new TestReporter();
        var graph = new TaskGraph { ProgressReporter = reporter };
        graph.Add("work", async () => await Task.Delay(10));

        await graph.RunAsync();

        Assert.Single(reporter.Completed);
        Assert.True(reporter.Completed[0].Elapsed > TimeSpan.Zero);
    }

    [Fact]
    public void ConsoleProgressReporter_DoesNotThrow()
    {
        var reporter = new ConsoleProgressReporter();
        reporter.OnTaskStarted("test");
        reporter.OnTaskCompleted("test", TimeSpan.FromMilliseconds(100));
        reporter.OnTaskFailed("test", new InvalidOperationException("fail"));
    }
}
