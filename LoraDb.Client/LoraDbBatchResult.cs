namespace LoraDb.Client;

/// <summary>
/// The aggregated result of a <see cref="LoraDbBatch"/> execution.
/// Contains one <see cref="LoraDbQueryResult"/> per statement, in execution order.
/// </summary>
/// <remarks>
/// Always dispose this instance when you are finished reading the results so that
/// the underlying <see cref="System.Text.Json.JsonDocument"/> instances held by each
/// <see cref="LoraDbQueryResult"/> are released.
/// </remarks>
public sealed class LoraDbBatchResult : IDisposable
{
    private bool _disposed;

    internal LoraDbBatchResult(IReadOnlyList<LoraDbQueryResult> results)
    {
        Results = results ?? throw new ArgumentNullException(nameof(results));
    }

    /// <summary>
    /// The individual query results, one per statement, in the order they were executed.
    /// </summary>
    public IReadOnlyList<LoraDbQueryResult> Results { get; }

    /// <summary>
    /// Disposes all contained <see cref="LoraDbQueryResult"/> instances.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var result in Results)
            result.Dispose();
    }
}
