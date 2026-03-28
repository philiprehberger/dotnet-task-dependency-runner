using Xunit;
namespace Philiprehberger.TaskDependencyRunner.Tests;

public class DryRunTests
{
    [Fact]
    public void DryRun_ReturnsBatchesGroupedByDependencyLevel()
    {
        var graph = new TaskGraph()
            .Add("a", () => { })
            .Add("b", () => { })
            .Add("c", () => { }, "a", "b")
            .Add("d", () => { }, "c");

        var plan = graph.DryRun();

        Assert.Equal(3, plan.Batches.Count);
        Assert.Contains("a", plan.Batches[0]);
        Assert.Contains("b", plan.Batches[0]);
        Assert.Equal(new[] { "c" }, plan.Batches[1]);
        Assert.Equal(new[] { "d" }, plan.Batches[2]);
    }

    [Fact]
    public void DryRun_ReturnsCorrectTaskCount()
    {
        var graph = new TaskGraph()
            .Add("a", () => { })
            .Add("b", () => { }, "a");

        var plan = graph.DryRun();

        Assert.Equal(2, plan.TaskCount);
    }

    [Fact]
    public void DryRun_ReturnsFlatOrder()
    {
        var graph = new TaskGraph()
            .Add("b", () => { }, "a")
            .Add("a", () => { });

        var plan = graph.DryRun();

        Assert.Equal(new[] { "a", "b" }, plan.Order);
    }

    [Fact]
    public void DryRun_ThrowsOnCircularDependency()
    {
        var graph = new TaskGraph()
            .Add("a", () => { }, "b")
            .Add("b", () => { }, "a");

        Assert.Throws<CircularDependencyException>(() => graph.DryRun());
    }

    [Fact]
    public void DryRun_DoesNotExecuteTasks()
    {
        var executed = false;

        var graph = new TaskGraph()
            .Add("a", () => { executed = true; });

        graph.DryRun();

        Assert.False(executed);
    }
}
