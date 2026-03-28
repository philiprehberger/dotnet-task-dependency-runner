namespace Philiprehberger.TaskDependencyRunner;

/// <summary>
/// Reports progress during task graph execution.
/// </summary>
public interface IProgressReporter
{
    /// <summary>
    /// Called when a task begins execution.
    /// </summary>
    /// <param name="name">The name of the task.</param>
    void OnTaskStarted(string name);

    /// <summary>
    /// Called when a task completes successfully.
    /// </summary>
    /// <param name="name">The name of the task.</param>
    /// <param name="elapsed">The time taken to execute the task.</param>
    void OnTaskCompleted(string name, TimeSpan elapsed);

    /// <summary>
    /// Called when a task fails with an exception.
    /// </summary>
    /// <param name="name">The name of the task.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    void OnTaskFailed(string name, Exception exception);
}

/// <summary>
/// A progress reporter that writes task status to the console.
/// </summary>
public sealed class ConsoleProgressReporter : IProgressReporter
{
    /// <inheritdoc />
    public void OnTaskStarted(string name)
    {
        Console.WriteLine($"[START] {name}");
    }

    /// <inheritdoc />
    public void OnTaskCompleted(string name, TimeSpan elapsed)
    {
        Console.WriteLine($"[DONE]  {name} ({elapsed.TotalMilliseconds:F0}ms)");
    }

    /// <inheritdoc />
    public void OnTaskFailed(string name, Exception exception)
    {
        Console.WriteLine($"[FAIL]  {name}: {exception.Message}");
    }
}
