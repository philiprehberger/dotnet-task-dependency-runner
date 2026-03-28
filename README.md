# Philiprehberger.TaskDependencyRunner

[![CI](https://github.com/philiprehberger/dotnet-task-dependency-runner/actions/workflows/ci.yml/badge.svg)](https://github.com/philiprehberger/dotnet-task-dependency-runner/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Philiprehberger.TaskDependencyRunner.svg)](https://www.nuget.org/packages/Philiprehberger.TaskDependencyRunner)
[![GitHub release](https://img.shields.io/github/v/release/philiprehberger/dotnet-task-dependency-runner)](https://github.com/philiprehberger/dotnet-task-dependency-runner/releases)
[![Last updated](https://img.shields.io/github/last-commit/philiprehberger/dotnet-task-dependency-runner)](https://github.com/philiprehberger/dotnet-task-dependency-runner/commits/main)
[![License](https://img.shields.io/github/license/philiprehberger/dotnet-task-dependency-runner)](LICENSE)
[![Bug Reports](https://img.shields.io/github/issues/philiprehberger/dotnet-task-dependency-runner/bug)](https://github.com/philiprehberger/dotnet-task-dependency-runner/issues?q=is%3Aissue+is%3Aopen+label%3Abug)
[![Feature Requests](https://img.shields.io/github/issues/philiprehberger/dotnet-task-dependency-runner/enhancement)](https://github.com/philiprehberger/dotnet-task-dependency-runner/issues?q=is%3Aissue+is%3Aopen+label%3Aenhancement)
[![Sponsor](https://img.shields.io/badge/sponsor-GitHub%20Sponsors-ec6cb9)](https://github.com/sponsors/philiprehberger)

Lightweight task runner with dependency graph resolution and parallel execution.

## Installation

```bash
dotnet add package Philiprehberger.TaskDependencyRunner
```

## Usage

```csharp
using Philiprehberger.TaskDependencyRunner;

var graph = new TaskGraph()
    .Add("clean",   () => Console.WriteLine("Cleaning..."))
    .Add("restore", () => Console.WriteLine("Restoring packages..."), "clean")
    .Add("build",   () => Console.WriteLine("Building..."),           "restore")
    .Add("test",    () => Console.WriteLine("Running tests..."),       "build")
    .Add("pack",    () => Console.WriteLine("Packing..."),             "build");

await graph.RunAsync();
```

### Typed task results

Tasks can produce results that downstream tasks consume via `ITaskContext`:

```csharp
using Philiprehberger.TaskDependencyRunner;

var graph = new TaskGraph()
    .Add<int>("fetch-count", _ => Task.FromResult(42))
    .Add<string>("format", ctx =>
    {
        var count = ctx.GetResult<int>("fetch-count");
        return Task.FromResult($"Total: {count}");
    }, "fetch-count")
    .Add("print", (ctx) =>
    {
        Console.WriteLine(ctx.GetResult<string>("format"));
        return Task.CompletedTask;
    }, "format");

await graph.RunAsync();

// Access results after execution
var formatted = (string)graph.TaskResults["format"]!;
```

### Per-task timeout

Set a timeout on individual tasks. If the timeout elapses, a `TaskTimeoutException` is thrown:

```csharp
var graph = new TaskGraph()
    .Add("fast", () => Console.WriteLine("Done"))
    .Add("slow", async () => await Task.Delay(10_000), timeout: TimeSpan.FromSeconds(2));

try
{
    await graph.RunAsync();
}
catch (TaskTimeoutException ex)
{
    Console.WriteLine($"{ex.TaskName} timed out");
}
```

### Progress reporting

Track execution progress with the `IProgressReporter` interface:

```csharp
var graph = new TaskGraph
{
    ProgressReporter = new ConsoleProgressReporter()
};

graph
    .Add("clean", () => { })
    .Add("build", () => { }, "clean")
    .Add("test",  () => { }, "build");

await graph.RunAsync();
// Output:
// [START] clean
// [DONE]  clean (1ms)
// [START] build
// [DONE]  build (0ms)
// [START] test
// [DONE]  test (0ms)
```

### Dry-run mode

Validate the graph and inspect the execution plan without running any tasks:

```csharp
var graph = new TaskGraph()
    .Add("a", () => { })
    .Add("b", () => { })
    .Add("c", () => { }, "a", "b")
    .Add("d", () => { }, "c");

var plan = graph.DryRun();

foreach (var (batch, i) in plan.Batches.Select((b, i) => (b, i)))
    Console.WriteLine($"Batch {i}: {string.Join(", ", batch)}");
// Batch 0: a, b
// Batch 1: c
// Batch 2: d
```

### Max concurrency

Limit how many tasks run in parallel:

```csharp
var graph = new TaskGraph { MaxConcurrency = 2 };

graph
    .Add("a", async () => await Task.Delay(100))
    .Add("b", async () => await Task.Delay(100))
    .Add("c", async () => await Task.Delay(100));

await graph.RunAsync();
```

### Error handling

```csharp
// Circular dependency
var bad = new TaskGraph()
    .Add("a", () => { }, "b")
    .Add("b", () => { }, "a");
// Throws CircularDependencyException

// Missing dependency
var missing = new TaskGraph()
    .Add("a", () => { }, "nonexistent");
// Throws MissingDependencyException
```

## API

### `TaskGraph`

| Member | Description |
|--------|-------------|
| `Add(name, Action, params string[])` | Register a synchronous task |
| `Add(name, Func<Task>, params string[])` | Register an async task |
| `Add(name, Func<ITaskContext, Task>, params string[])` | Register an async task with context access |
| `Add<TResult>(name, Func<ITaskContext, Task<TResult>>, params string[])` | Register a typed result task |
| `Add(name, action, TimeSpan?, params string[])` | Register a task with timeout (sync or async) |
| `GetExecutionOrder()` | Return names in topological order |
| `RunAsync(CancellationToken)` | Execute all tasks; independent tasks run in parallel |
| `DryRun()` | Validate graph and return `ExecutionPlan` without executing |
| `MaxConcurrency` | Max parallel tasks (0 = unlimited, default) |
| `OnTaskCompleted` | `Action<string, int, int>?` callback (taskName, completedCount, totalCount) |
| `ProgressReporter` | `IProgressReporter?` for detailed start/complete/fail events |
| `TaskResults` | `IReadOnlyDictionary<string, object?>` of typed task results |

### `ITaskContext`

| Method | Description |
|--------|-------------|
| `GetResult<T>(taskName)` | Retrieve a dependency's typed result |
| `HasResult(taskName)` | Check whether a result exists for the given task |

### `IProgressReporter`

| Method | Description |
|--------|-------------|
| `OnTaskStarted(name)` | Called when a task begins execution |
| `OnTaskCompleted(name, elapsed)` | Called when a task completes successfully |
| `OnTaskFailed(name, exception)` | Called when a task fails |

### `ExecutionPlan`

| Property | Type | Description |
|----------|------|-------------|
| `Batches` | `IReadOnlyList<IReadOnlyList<string>>` | Ordered batches of parallelizable tasks |
| `Order` | `IReadOnlyList<string>` | Flat topological order |
| `TaskCount` | `int` | Total number of tasks |

### Exceptions

| Type | Description |
|------|-------------|
| `CircularDependencyException` | The graph contains a cycle |
| `MissingDependencyException` | A task depends on an unregistered name |
| `TaskTimeoutException` | A task exceeded its configured timeout (`TaskName` property) |

## Development

```bash
dotnet build src/Philiprehberger.TaskDependencyRunner.csproj --configuration Release
```

## Support

If you find this package useful, consider giving it a star on GitHub — it helps motivate continued maintenance and development.

[![LinkedIn](https://img.shields.io/badge/Philip%20Rehberger-LinkedIn-0A66C2?logo=linkedin)](https://www.linkedin.com/in/philiprehberger)
[![More packages](https://img.shields.io/badge/more-open%20source%20packages-blue)](https://philiprehberger.com/open-source-packages)

## License

[MIT](LICENSE)
