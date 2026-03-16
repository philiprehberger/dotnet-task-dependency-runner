# Philiprehberger.TaskDependencyRunner

[![CI](https://github.com/philiprehberger/dotnet-task-dependency-runner/actions/workflows/ci.yml/badge.svg)](https://github.com/philiprehberger/dotnet-task-dependency-runner/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Philiprehberger.TaskDependencyRunner.svg)](https://www.nuget.org/packages/Philiprehberger.TaskDependencyRunner)
[![License](https://img.shields.io/github/license/philiprehberger/dotnet-task-dependency-runner)](LICENSE)

A lightweight task runner that resolves a dependency graph and runs independent tasks in parallel. Uses Kahn's topological sort algorithm.

## Install

```bash
dotnet add package Philiprehberger.TaskDependencyRunner
```

## Usage

```csharp
using Philiprehberger.TaskDependencyRunner;

var graph = new TaskGraph()
    .Add("clean",   () => Console.WriteLine("Cleaning..."))
    .Add("restore", () => Console.WriteLine("Restoring packages..."), dependsOn: "clean")
    .Add("build",   () => Console.WriteLine("Building..."),           dependsOn: "restore")
    .Add("test",    () => Console.WriteLine("Running tests..."),       dependsOn: "build")
    .Add("pack",    () => Console.WriteLine("Packing..."),             dependsOn: "build");

// Inspect order
var order = graph.GetExecutionOrder();
Console.WriteLine(string.Join(" -> ", order));
// clean -> restore -> build -> test -> pack
// (test and pack may run in parallel)

// Execute
await graph.RunAsync();
```

### Async tasks

```csharp
var graph = new TaskGraph()
    .Add("fetch", async () =>
    {
        await Task.Delay(100);
        Console.WriteLine("Fetched data");
    })
    .Add("process", async () =>
    {
        await Task.Delay(50);
        Console.WriteLine("Processed");
    }, dependsOn: "fetch");

await graph.RunAsync();
```

### Max concurrency

Limit how many tasks run in parallel to avoid overwhelming resources:

```csharp
var graph = new TaskGraph { MaxConcurrency = 2 };

graph
    .Add("a", async () => await Task.Delay(100))
    .Add("b", async () => await Task.Delay(100))
    .Add("c", async () => await Task.Delay(100))
    .Add("d", async () => await Task.Delay(100));

// At most 2 tasks will execute simultaneously
await graph.RunAsync();
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

Track execution progress with the `OnTaskCompleted` callback:

```csharp
var graph = new TaskGraph
{
    OnTaskCompleted = (name, done, total) =>
        Console.WriteLine($"[{done}/{total}] {name} completed")
};

graph
    .Add("clean",   () => { })
    .Add("build",   () => { }, dependsOn: "clean")
    .Add("test",    () => { }, dependsOn: "build");

await graph.RunAsync();
// Output:
// [1/3] clean completed
// [2/3] build completed
// [3/3] test completed
```

### Error handling

```csharp
// Circular dependency
var bad = new TaskGraph()
    .Add("a", () => {}, dependsOn: "b")
    .Add("b", () => {}, dependsOn: "a");

// Throws CircularDependencyException
await bad.RunAsync();

// Missing dependency
var missing = new TaskGraph()
    .Add("a", () => {}, dependsOn: "nonexistent");

// Throws MissingDependencyException
await missing.RunAsync();
```

## API

### `TaskGraph`

| Member | Description |
|--------|-------------|
| `Add(name, Action, params string[] dependsOn)` | Register a synchronous task |
| `Add(name, Func<Task>, params string[] dependsOn)` | Register an async task |
| `Add(name, Action, TimeSpan? timeout, params string[] dependsOn)` | Register a synchronous task with timeout |
| `Add(name, Func<Task>, TimeSpan? timeout, params string[] dependsOn)` | Register an async task with timeout |
| `GetExecutionOrder()` | Return names in topological order |
| `RunAsync(CancellationToken)` | Execute all tasks; independent tasks run in parallel |
| `MaxConcurrency` | Max parallel tasks (0 = unlimited, default) |
| `OnTaskCompleted` | `Action<string, int, int>?` callback (taskName, completedCount, totalCount) |

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

## License

MIT
