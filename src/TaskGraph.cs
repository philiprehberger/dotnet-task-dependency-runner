namespace Philiprehberger.TaskDependencyRunner;

/// <summary>
/// Thrown when the task graph contains a cycle.
/// </summary>
public sealed class CircularDependencyException : Exception
{
    public CircularDependencyException()
        : base("A circular dependency was detected in the task graph.") { }

    public CircularDependencyException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a task declares a dependency on a name that has not been registered.
/// </summary>
public sealed class MissingDependencyException : Exception
{
    public MissingDependencyException()
        : base("A task references a dependency that does not exist.") { }

    public MissingDependencyException(string message) : base(message) { }
}

/// <summary>
/// A lightweight task runner that resolves a dependency graph and executes tasks
/// in topological order, running independent tasks in parallel.
/// </summary>
public sealed class TaskGraph
{
    private sealed record TaskEntry(
        string Name,
        Func<Task> Action,
        IReadOnlyList<string> DependsOn
    );

    private readonly Dictionary<string, TaskEntry> _tasks =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Registers an async task.
    /// </summary>
    public TaskGraph Add(string name, Func<Task> action, params string[] dependsOn)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(action);
        _tasks[name] = new TaskEntry(name, action, dependsOn ?? Array.Empty<string>());
        return this;
    }

    /// <summary>
    /// Registers a synchronous task.
    /// </summary>
    public TaskGraph Add(string name, Action action, params string[] dependsOn)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Add(name, () => { action(); return Task.CompletedTask; }, dependsOn);
    }

    /// <summary>
    /// Returns the names of all tasks in a valid execution order (Kahn's algorithm).
    /// Throws <see cref="CircularDependencyException"/> if a cycle is detected.
    /// Throws <see cref="MissingDependencyException"/> if an unknown dependency is referenced.
    /// </summary>
    public IReadOnlyList<string> GetExecutionOrder()
    {
        ValidateDependencies();
        return TopologicalSort();
    }

    /// <summary>
    /// Runs all tasks in dependency order, executing independent tasks in parallel.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        ValidateDependencies();

        // Build in-degree map and adjacency list
        var inDegree = new Dictionary<string, int>(StringComparer.Ordinal);
        var dependents = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var name in _tasks.Keys)
        {
            inDegree[name] = 0;
            dependents[name] = new List<string>();
        }

        foreach (var entry in _tasks.Values)
        {
            foreach (var dep in entry.DependsOn)
            {
                dependents[dep].Add(entry.Name);
                inDegree[entry.Name]++;
            }
        }

        var completed = new HashSet<string>(StringComparer.Ordinal);
        var running = new Dictionary<string, Task>(StringComparer.Ordinal);

        while (completed.Count < _tasks.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Find tasks that are ready to run (all deps complete, not yet started)
            var ready = _tasks.Keys
                .Where(name =>
                    !completed.Contains(name) &&
                    !running.ContainsKey(name) &&
                    _tasks[name].DependsOn.All(d => completed.Contains(d)))
                .ToList();

            if (ready.Count == 0 && running.Count == 0)
                throw new CircularDependencyException();

            // Start all ready tasks in parallel
            foreach (var name in ready)
                running[name] = _tasks[name].Action();

            // Wait for any task to complete
            if (running.Count > 0)
            {
                var done = await Task.WhenAny(running.Values);
                await done; // propagate exceptions

                var finishedName = running.First(kv => kv.Value == done).Key;
                running.Remove(finishedName);
                completed.Add(finishedName);
            }
        }

        // Await any remaining running tasks
        foreach (var t in running.Values)
            await t;
    }

    private void ValidateDependencies()
    {
        foreach (var entry in _tasks.Values)
        {
            foreach (var dep in entry.DependsOn)
            {
                if (!_tasks.ContainsKey(dep))
                    throw new MissingDependencyException(
                        $"Task '{entry.Name}' depends on '{dep}', which has not been registered.");
            }
        }
    }

    private List<string> TopologicalSort()
    {
        var inDegree = new Dictionary<string, int>(StringComparer.Ordinal);
        var dependents = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var name in _tasks.Keys)
        {
            inDegree[name] = 0;
            dependents[name] = new List<string>();
        }

        foreach (var entry in _tasks.Values)
        {
            foreach (var dep in entry.DependsOn)
            {
                dependents[dep].Add(entry.Name);
                inDegree[entry.Name]++;
            }
        }

        var queue = new Queue<string>(
            inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key).OrderBy(k => k));

        var order = new List<string>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            order.Add(current);

            foreach (var dependent in dependents[current].OrderBy(k => k))
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                    queue.Enqueue(dependent);
            }
        }

        if (order.Count != _tasks.Count)
            throw new CircularDependencyException();

        return order;
    }
}
