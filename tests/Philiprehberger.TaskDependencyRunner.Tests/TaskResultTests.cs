using Xunit;
namespace Philiprehberger.TaskDependencyRunner.Tests;

public class TaskResultTests
{
    [Fact]
    public async Task TypedTask_StoresResultInTaskResults()
    {
        var graph = new TaskGraph()
            .Add<int>("compute", _ => Task.FromResult(42));

        await graph.RunAsync();

        Assert.Equal(42, graph.TaskResults["compute"]);
    }

    [Fact]
    public async Task DependentTask_CanAccessDependencyResult()
    {
        int? capturedValue = null;

        var graph = new TaskGraph()
            .Add<int>("producer", _ => Task.FromResult(100))
            .Add("consumer", async (ctx) =>
            {
                capturedValue = ctx.GetResult<int>("producer");
                await Task.CompletedTask;
            }, "producer");

        await graph.RunAsync();

        Assert.Equal(100, capturedValue);
    }

    [Fact]
    public async Task ChainedResults_PropagateCorrectly()
    {
        var graph = new TaskGraph()
            .Add<int>("step1", _ => Task.FromResult(10))
            .Add<int>("step2", ctx => Task.FromResult(ctx.GetResult<int>("step1") * 2), "step1")
            .Add<string>("step3", ctx => Task.FromResult($"Result: {ctx.GetResult<int>("step2")}"), "step2");

        await graph.RunAsync();

        Assert.Equal(10, graph.TaskResults["step1"]);
        Assert.Equal(20, graph.TaskResults["step2"]);
        Assert.Equal("Result: 20", graph.TaskResults["step3"]);
    }

    [Fact]
    public async Task HasResult_ReturnsFalseForNonExistentTask()
    {
        var hasResult = false;

        var graph = new TaskGraph()
            .Add("check", (ctx) =>
            {
                hasResult = ctx.HasResult("nonexistent");
                return Task.CompletedTask;
            });

        await graph.RunAsync();

        Assert.False(hasResult);
    }

    [Fact]
    public async Task GetResult_ThrowsKeyNotFoundException_WhenTaskNotFound()
    {
        Exception? caught = null;

        var graph = new TaskGraph()
            .Add("check", (ctx) =>
            {
                try
                {
                    ctx.GetResult<int>("nonexistent");
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
                return Task.CompletedTask;
            });

        await graph.RunAsync();

        Assert.IsType<KeyNotFoundException>(caught);
    }
}
