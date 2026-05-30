namespace LoraDb.Client;

/// <summary>
/// A lightweight sequential executor that runs a set of Cypher statements one after
/// another against the same <see cref="ILoraDbClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// LoraDB uses auto-commit semantics: every query is its own implicit transaction.
/// A <see cref="LoraDbBatch"/> therefore provides <em>fail-fast</em> sequential
/// execution — if any statement throws, the remaining statements are not executed and
/// the exception is propagated to the caller.
/// </para>
/// <para>
/// Use <see cref="LoraDbClientCrudExtensions.CreateBatch"/> to obtain an instance from
/// an <see cref="ILoraDbClient"/>.
/// </para>
/// </remarks>
public sealed class LoraDbBatch
{
    private readonly ILoraDbClient _client;

    private readonly List<(string query, IReadOnlyDictionary<string, object?>? parameters, string format)> _statements
        = new();

    /// <summary>
    /// Initialises a new <see cref="LoraDbBatch"/> bound to the given
    /// <paramref name="client"/>.
    /// </summary>
    public LoraDbBatch(ILoraDbClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Gets the number of statements currently queued in the batch.
    /// </summary>
    public int Count => _statements.Count;

    /// <summary>
    /// Appends a Cypher statement to the batch.
    /// </summary>
    /// <param name="query">The Cypher query to execute.</param>
    /// <param name="parameters">Optional query parameters.</param>
    /// <param name="format">Response format (default: <c>"rows"</c>).</param>
    /// <returns>This <see cref="LoraDbBatch"/> instance to allow fluent chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="query"/> is null or whitespace.</exception>
    public LoraDbBatch Add(
        string query,
        IReadOnlyDictionary<string, object?>? parameters = null,
        string format = Models.LoraDbQueryRequest.DefaultFormat)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be null or whitespace.", nameof(query));

        _statements.Add((query, parameters, format));
        return this;
    }

    /// <summary>
    /// Appends multiple Cypher statements to the batch, using the default format for each.
    /// </summary>
    /// <param name="queries">The Cypher queries to add.</param>
    /// <returns>This <see cref="LoraDbBatch"/> instance to allow fluent chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="queries"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Any query in <paramref name="queries"/> is null or whitespace.</exception>
    public LoraDbBatch AddRange(IEnumerable<string> queries)
    {
        if (queries is null)
            throw new ArgumentNullException(nameof(queries));

        foreach (var query in queries)
            Add(query);

        return this;
    }

    /// <summary>
    /// Appends multiple Cypher statements (with optional parameters and format) to the batch.
    /// </summary>
    /// <param name="statements">
    /// A sequence of <c>(query, parameters, format)</c> tuples.
    /// <c>parameters</c> may be <c>null</c>; <c>format</c> defaults to the standard rows format
    /// when omitted (pass <see cref="Models.LoraDbQueryRequest.DefaultFormat"/> or leave the tuple field as
    /// <c>null</c>).
    /// </param>
    /// <returns>This <see cref="LoraDbBatch"/> instance to allow fluent chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="statements"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Any query in <paramref name="statements"/> is null or whitespace.</exception>
    public LoraDbBatch AddRange(
        IEnumerable<(string query, IReadOnlyDictionary<string, object?>? parameters, string format)> statements)
    {
        if (statements is null)
            throw new ArgumentNullException(nameof(statements));

        foreach (var (query, parameters, format) in statements)
            Add(query, parameters, format);

        return this;
    }

    /// <summary>
    /// Executes all queued statements in order and returns a
    /// <see cref="LoraDbBatchResult"/> containing the individual results.
    /// </summary>
    /// <remarks>
    /// Execution stops on the first statement that throws an exception.
    /// Any results already collected before the failure are disposed and the
    /// exception is re-thrown.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel execution.</param>
    /// <returns>
    /// A <see cref="LoraDbBatchResult"/> with one entry per statement.
    /// The caller is responsible for disposing it.
    /// </returns>
    public async Task<LoraDbBatchResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<LoraDbQueryResult>(_statements.Count);
        try
        {
            foreach (var (query, parameters, format) in _statements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await _client.ExecuteAsync(query, parameters, format, cancellationToken).ConfigureAwait(false);
                results.Add(result);
            }
        }
        catch
        {
            foreach (var r in results) r.Dispose();
            throw;
        }

        return new LoraDbBatchResult(results);
    }
}
