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
}
