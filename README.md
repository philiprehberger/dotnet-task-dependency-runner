# Philiprehberger.TaskDependencyRunner

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

| Method | Description |
|--------|-------------|
| `Add(name, Action, params string[] dependsOn)` | Register a synchronous task |
| `Add(name, Func<Task>, params string[] dependsOn)` | Register an async task |
| `GetExecutionOrder()` | Return names in topological order |
| `RunAsync(CancellationToken)` | Execute all tasks; independent tasks run in parallel |

### Exceptions

| Type | Description |
|------|-------------|
| `CircularDependencyException` | The graph contains a cycle |
| `MissingDependencyException` | A task depends on an unregistered name |

## License

MIT
