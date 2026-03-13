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
/// Thrown when a task exceeds its configured timeout.
/// </summary>
public sealed class TaskTimeoutException : Exception
{
    /// <summary>
    /// The name of the task that timed out.
    /// </summary>
    public string TaskName { get; }

    public TaskTimeoutException(string taskName)
        : base($"Task '{taskName}' exceeded its timeout.")
    {
        TaskName = taskName;
    }

    public TaskTimeoutException(string taskName, string message) : base(message)
    {
        TaskName = taskName;
    }
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
        IReadOnlyList<string> DependsOn,
        TimeSpan? Timeout
    );

    private readonly Dictionary<string, TaskEntry> _tasks =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Maximum number of tasks to execute concurrently. 0 means unlimited (default).
    /// </summary>
    public int MaxConcurrency { get; set; }

    /// <summary>
    /// Optional callback invoked after each task completes.
    /// Parameters: task name, completed count, total count.
    /// </summary>
    public Action<string, int, int>? OnTaskCompleted { get; set; }

    /// <summary>
    /// Registers an async task.
    /// </summary>
    public TaskGraph Add(string name, Func<Task> action, params string[] dependsOn)
    {
        return Add(name, action, timeout: null, dependsOn);
    }

    /// <summary>
    /// Registers an async task with an optional timeout.
    /// </summary>
    public TaskGraph Add(string name, Func<Task> action, TimeSpan? timeout, params string[] dependsOn)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(action);
        _tasks[name] = new TaskEntry(name, action, dependsOn ?? Array.Empty<string>(), timeout);
        return this;
    }

    /// <summary>
    /// Registers a synchronous task.
    /// </summary>
    public TaskGraph Add(string name, Action action, params string[] dependsOn)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Add(name, () => { action(); return Task.CompletedTask; }, timeout: null, dependsOn);
    }

    /// <summary>
    /// Registers a synchronous task with an optional timeout.
    /// </summary>
    public TaskGraph Add(string name, Action action, TimeSpan? timeout, params string[] dependsOn)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Add(name, () => { action(); return Task.CompletedTask; }, timeout, dependsOn);
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

        var totalCount = _tasks.Count;
        var completedCount = 0;

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

        SemaphoreSlim? semaphore = MaxConcurrency > 0 ? new SemaphoreSlim(MaxConcurrency) : null;

        try
        {
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

                // Start all ready tasks in parallel (respecting concurrency limit)
                foreach (var name in ready)
                {
                    var entry = _tasks[name];
                    running[name] = RunTaskAsync(entry, semaphore, cancellationToken);
                }

                // Wait for any task to complete
                if (running.Count > 0)
                {
                    var done = await Task.WhenAny(running.Values);
                    await done; // propagate exceptions

                    var finishedName = running.First(kv => kv.Value == done).Key;
                    running.Remove(finishedName);
                    completed.Add(finishedName);

                    completedCount++;
                    OnTaskCompleted?.Invoke(finishedName, completedCount, totalCount);
                }
            }

            // Await any remaining running tasks
            foreach (var t in running.Values)
                await t;
        }
        finally
        {
            semaphore?.Dispose();
        }
    }

    private static async Task RunTaskAsync(TaskEntry entry, SemaphoreSlim? semaphore, CancellationToken cancellationToken)
    {
        if (semaphore != null)
            await semaphore.WaitAsync(cancellationToken);

        try
        {
            if (entry.Timeout.HasValue)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(entry.Timeout.Value);

                try
                {
                    var task = entry.Action();
                    await task.WaitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TaskTimeoutException(entry.Name);
                }
            }
            else
            {
                await entry.Action();
            }
        }
        finally
        {
            semaphore?.Release();
        }
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
