using System.Runtime.CompilerServices;
using System.Text.Json.Serialization.Metadata;

namespace LoraDb.Client;

public static class LoraDbClientTypedExtensions
{
    public static async Task<IReadOnlyList<T>> ExecuteRowsAsync<T>(
        this ILoraDbClient client,
        string query,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (client is null)
            throw new ArgumentNullException(nameof(client));

        using var result = await client.ExecuteAsync(query, parameters, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.ReadRows<T>();
    }

    public static async Task<IReadOnlyList<T>> ExecuteRowsAsync<T>(
        this ILoraDbClient client,
        string query,
        JsonTypeInfo<T> typeInfo,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (client is null)
            throw new ArgumentNullException(nameof(client));
        if (typeInfo is null)
            throw new ArgumentNullException(nameof(typeInfo));

        using var result = await client.ExecuteAsync(query, parameters, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.ReadRows(typeInfo);
    }

    /// <summary>
    /// Executes the given Cypher query and streams the rows as an <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    /// <remarks>
    /// The entire response is fetched in a single round-trip. The rows are then yielded one
    /// at a time, which allows callers to use <c>await foreach</c> and LINQ-style lazy processing
    /// without materialising the full list first.
    /// </remarks>
    public static async IAsyncEnumerable<T> ExecuteRowsStreamAsync<T>(
        this ILoraDbClient client,
        string query,
        IReadOnlyDictionary<string, object?>? parameters = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (client is null)
            throw new ArgumentNullException(nameof(client));

        using var result = await client.ExecuteAsync(query, parameters, cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (var row in result.ReadRows<T>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }
    }

    /// <summary>
    /// Executes the given Cypher query and streams the rows as an <see cref="IAsyncEnumerable{T}"/>,
    /// using source-generated JSON metadata for deserialization.
    /// </summary>
    public static async IAsyncEnumerable<T> ExecuteRowsStreamAsync<T>(
        this ILoraDbClient client,
        string query,
        JsonTypeInfo<T> typeInfo,
        IReadOnlyDictionary<string, object?>? parameters = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (client is null)
            throw new ArgumentNullException(nameof(client));
        if (typeInfo is null)
            throw new ArgumentNullException(nameof(typeInfo));

        using var result = await client.ExecuteAsync(query, parameters, cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (var row in result.ReadRows(typeInfo))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }
    }
}
