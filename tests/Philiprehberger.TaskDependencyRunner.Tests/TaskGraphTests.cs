using Xunit;
namespace Philiprehberger.TaskDependencyRunner.Tests;

public class TaskGraphTests
{
    [Fact]
    public void GetExecutionOrder_ReturnsTopologicalOrder()
    {
        var graph = new TaskGraph()
            .Add("c", () => { }, "b")
            .Add("b", () => { }, "a")
            .Add("a", () => { });

        var order = graph.GetExecutionOrder();

        Assert.Equal(new[] { "a", "b", "c" }, order);
    }

    [Fact]
    public void GetExecutionOrder_ThrowsOnCircularDependency()
    {
        var graph = new TaskGraph()
            .Add("a", () => { }, "b")
            .Add("b", () => { }, "a");

        Assert.Throws<CircularDependencyException>(() => graph.GetExecutionOrder());
    }

    [Fact]
    public void GetExecutionOrder_ThrowsOnMissingDependency()
    {
        var graph = new TaskGraph()
            .Add("a", () => { }, "nonexistent");

        Assert.Throws<MissingDependencyException>(() => graph.GetExecutionOrder());
    }

    [Fact]
    public async Task RunAsync_ExecutesAllTasks()
    {
        var executed = new List<string>();

        var graph = new TaskGraph()
            .Add("a", () => executed.Add("a"))
            .Add("b", () => executed.Add("b"), "a")
            .Add("c", () => executed.Add("c"), "a");

        await graph.RunAsync();

        Assert.Contains("a", executed);
        Assert.Contains("b", executed);
        Assert.Contains("c", executed);
        Assert.Equal(0, executed.IndexOf("a"));
    }

    [Fact]
    public async Task RunAsync_RespectsMaxConcurrency()
    {
        var maxConcurrent = 0;
        var currentConcurrent = 0;
        var lockObj = new object();

        var graph = new TaskGraph { MaxConcurrency = 2 };
        for (var i = 0; i < 4; i++)
        {
            var name = $"task{i}";
            graph.Add(name, async () =>
            {
                int current;
                lock (lockObj)
                {
                    currentConcurrent++;
                    current = currentConcurrent;
                    if (current > maxConcurrent) maxConcurrent = current;
                }
                await Task.Delay(50);
                lock (lockObj) { currentConcurrent--; }
            });
        }

        await graph.RunAsync();

        Assert.True(maxConcurrent <= 2);
    }
}
