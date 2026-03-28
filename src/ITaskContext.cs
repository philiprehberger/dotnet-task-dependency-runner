namespace Philiprehberger.TaskDependencyRunner;

/// <summary>
/// Provides access to results produced by previously completed tasks.
/// </summary>
public interface ITaskContext
{
    /// <summary>
    /// Retrieves the result of a previously completed task by name.
    /// </summary>
    /// <typeparam name="T">The expected result type.</typeparam>
    /// <param name="taskName">The name of the completed task.</param>
    /// <returns>The result produced by the specified task.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no result exists for the given task name.</exception>
    /// <exception cref="InvalidCastException">Thrown when the result cannot be cast to <typeparamref name="T"/>.</exception>
    T GetResult<T>(string taskName);

    /// <summary>
    /// Checks whether a result exists for the given task name.
    /// </summary>
    /// <param name="taskName">The name of the task.</param>
    /// <returns><c>true</c> if a result has been stored for the task; otherwise <c>false</c>.</returns>
    bool HasResult(string taskName);
}

/// <summary>
/// Default implementation of <see cref="ITaskContext"/> backed by a dictionary.
/// </summary>
internal sealed class TaskContext : ITaskContext
{
    internal readonly Dictionary<string, object?> _results = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public T GetResult<T>(string taskName)
    {
        if (!_results.TryGetValue(taskName, out var value))
            throw new KeyNotFoundException($"No result found for task '{taskName}'.");

        return (T)value!;
    }

    /// <inheritdoc />
    public bool HasResult(string taskName)
    {
        return _results.ContainsKey(taskName);
    }

    /// <summary>
    /// Stores a result for the given task name.
    /// </summary>
    internal void SetResult(string taskName, object? value)
    {
        _results[taskName] = value;
    }
}
