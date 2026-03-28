namespace Philiprehberger.TaskDependencyRunner;

/// <summary>
/// Represents the execution plan produced by a dry run. Contains ordered batches
/// of tasks where each batch can execute in parallel.
/// </summary>
public sealed class ExecutionPlan
{
    /// <summary>
    /// Ordered list of task batches. Tasks within the same batch have no
    /// dependencies on each other and can run concurrently.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> Batches { get; }

    /// <summary>
    /// All task names in a flat topological order.
    /// </summary>
    public IReadOnlyList<string> Order { get; }

    /// <summary>
    /// Total number of tasks in the plan.
    /// </summary>
    public int TaskCount { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutionPlan"/> class.
    /// </summary>
    /// <param name="batches">The ordered list of parallel task batches.</param>
    /// <param name="order">The flat topological order of all task names.</param>
    public ExecutionPlan(IReadOnlyList<IReadOnlyList<string>> batches, IReadOnlyList<string> order)
    {
        Batches = batches;
        Order = order;
        TaskCount = order.Count;
    }
}
