using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace LoraDb.Client;

/// <summary>
/// Extension methods on <see cref="ILoraDbClient"/> that provide structured helpers for
/// common Create / Read / Update / Delete node operations, building the required Cypher
/// queries automatically from structured inputs.
/// </summary>
/// <remarks>
/// <para>
/// All methods that return typed rows use the Cypher alias <c>n</c> (e.g.
/// <c>RETURN n</c>).  The row shape for node queries is therefore
/// <c>{"n":{"id":…,"labels":[…],"properties":{…}}}</c>, so the type parameter
/// <typeparamref name="T"/> should have a property named <c>n</c> (or mapped with
/// <see cref="JsonPropertyNameAttribute"/>) that holds the node data.
/// </para>
/// <para>
/// To create a batch that executes multiple statements sequentially, use
/// <see cref="CreateBatch"/>.
/// </para>
/// </remarks>
public static class LoraDbClientCrudExtensions
{
    // ── Create ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a node with the given <paramref name="label"/> and
    /// <paramref name="properties"/> and returns the first created row deserialized as
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// Generated Cypher (example):
    /// <code>CREATE (n:Person {name: $create_name, age: $create_age}) RETURN n</code>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="properties"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="label"/> is null/whitespace, or <paramref name="properties"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">
    /// The database returned no rows. This indicates an unexpected database state because a
    /// <c>CREATE … RETURN n</c> query always returns the created node on success.
    /// </exception>
    public static async ValueTask<T> CreateNodeAsync<T>(
        this ILoraDbClient client,
        string label,
        IReadOnlyDictionary<string, object?> properties,
        CancellationToken cancellationToken = default)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        ValidateLabel(label);
        if (properties is null) throw new ArgumentNullException(nameof(properties));
        if (properties.Count == 0) throw new ArgumentException("At least one property is required.", nameof(properties));

        var (propMap, parameters) = BuildPropertyMap(properties, "create", nameof(properties));
        var query = $"CREATE (n:{label} {propMap}) RETURN n";

        using var result = await client.ExecuteAsync(query, parameters, cancellationToken: cancellationToken).ConfigureAwait(false);
        var rows = result.ReadRows<T>();
        if (rows.Count == 0)
            throw new InvalidOperationException("CREATE did not return any rows.");
        return rows[0];
    }

    /// <summary>
    /// Creates a node with the given <paramref name="label"/> and no inline properties,
    /// returning the first created row deserialized as <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// Generated Cypher: <code>CREATE (n:Label) RETURN n</code>
    /// </remarks>
    public static async ValueTask<T> CreateNodeAsync<T>(
        this ILoraDbClient client,
        string label,
        CancellationToken cancellationToken = default)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        ValidateLabel(label);

        var query = $"CREATE (n:{label}) RETURN n";
        using var result = await client.ExecuteAsync(query, cancellationToken: cancellationToken).ConfigureAwait(false);
        var rows = result.ReadRows<T>();
        if (rows.Count == 0)
            throw new InvalidOperationException("CREATE did not return any rows.");
        return rows[0];
    }

    // ── Read ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds all nodes with the given <paramref name="label"/> matching the optional
    /// <paramref name="filters"/> and streams rows deserialized as <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// Generated Cypher (with filters):
    /// <code>MATCH (n:Person {name: $filter_name}) RETURN n</code>
    /// Generated Cypher (no filters):
    /// <code>MATCH (n:Person) RETURN n</code>
    /// </remarks>
    public static IAsyncEnumerable<T> FindNodesAsync<T>(
        this ILoraDbClient client,
        string label,
        IReadOnlyDictionary<string, object?>? filters = null,
        CancellationToken cancellationToken = default)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        ValidateLabel(label);

        string query;
        IReadOnlyDictionary<string, object?>? parameters = null;

        if (filters is { Count: > 0 })
        {
            var (propMap, p) = BuildPropertyMap(filters, "filter", nameof(filters));
            query = $"MATCH (n:{label} {propMap}) RETURN n";
            parameters = p;
        }
        else
        {
            query = $"MATCH (n:{label}) RETURN n";
        }

        return client.ExecuteRowsStreamAsync<T>(query, parameters, cancellationToken);
    }

    /// <summary>
    /// Finds the first node with the given <paramref name="label"/> matching the optional
    /// <paramref name="filters"/>, or <c>null</c> if no match exists.
    /// </summary>
    /// <remarks>
    /// Generated Cypher (with filters):
    /// <code>MATCH (n:Person {name: $filter_name}) RETURN n LIMIT 1</code>
    /// </remarks>
    public static async ValueTask<T?> FindNodeAsync<T>(
        this ILoraDbClient client,
        string label,
        IReadOnlyDictionary<string, object?>? filters = null,
        CancellationToken cancellationToken = default) where T : class
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        ValidateLabel(label);

        string query;
        IReadOnlyDictionary<string, object?>? parameters = null;

        if (filters is { Count: > 0 })
        {
            var (propMap, p) = BuildPropertyMap(filters, "filter", nameof(filters));
            query = $"MATCH (n:{label} {propMap}) RETURN n LIMIT 1";
            parameters = p;
        }
        else
        {
            query = $"MATCH (n:{label}) RETURN n LIMIT 1";
        }

        using var result = await client.ExecuteAsync(query, parameters, cancellationToken: cancellationToken).ConfigureAwait(false);
        var rows = result.ReadRows<T>();
        return rows.Count > 0 ? rows[0] : null;
    }

    // ── Update ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Matches nodes with the given <paramref name="label"/> and <paramref name="match"/>
    /// properties, applies the <paramref name="properties"/> via SET, and returns the
    /// updated node rows deserialized as <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// Generated Cypher (example):
    /// <code>MATCH (n:Person {id: $match_id}) SET n.age = $set_age RETURN n</code>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="match"/> or <paramref name="properties"/> is empty.</exception>
    public static IAsyncEnumerable<T> UpdateNodesAsync<T>(
        this ILoraDbClient client,
        string label,
        IReadOnlyDictionary<string, object?> match,
        IReadOnlyDictionary<string, object?> properties,
        CancellationToken cancellationToken = default)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        ValidateLabel(label);
        if (match is null) throw new ArgumentNullException(nameof(match));
        if (match.Count == 0) throw new ArgumentException("At least one match property is required.", nameof(match));
        if (properties is null) throw new ArgumentNullException(nameof(properties));
        if (properties.Count == 0) throw new ArgumentException("At least one property to set is required.", nameof(properties));

        var (matchMap, matchParams) = BuildPropertyMap(match, "match", nameof(match));
        var (setClause, setParams) = BuildSetClause(properties, "n", "set", nameof(properties));

        var allParams = new Dictionary<string, object?>(matchParams.Count + setParams.Count);
        foreach (var kv in matchParams) allParams[kv.Key] = kv.Value;
        foreach (var kv in setParams) allParams[kv.Key] = kv.Value;

        var query = $"MATCH (n:{label} {matchMap}) SET {setClause} RETURN n";

        return client.ExecuteRowsStreamAsync<T>(query, allParams, cancellationToken);
    }

    // ── Delete ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Deletes nodes with the given <paramref name="label"/> matching the optional
    /// <paramref name="match"/> properties. When <paramref name="detach"/> is <c>true</c>
    /// (the default), uses <c>DETACH DELETE</c> so all connected relationships are also removed.
    /// </summary>
    /// <remarks>
    /// Generated Cypher (with match and detach):
    /// <code>MATCH (n:Person {id: $match_id}) DETACH DELETE n</code>
    /// Generated Cypher (no match, no detach):
    /// <code>MATCH (n:Person) DELETE n</code>
    /// </remarks>
    public static async Task DeleteNodesAsync(
        this ILoraDbClient client,
        string label,
        IReadOnlyDictionary<string, object?>? match = null,
        bool detach = true,
        CancellationToken cancellationToken = default)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        ValidateLabel(label);

        string query;
        IReadOnlyDictionary<string, object?>? parameters = null;
        var deleteKeyword = detach ? "DETACH DELETE" : "DELETE";

        if (match is { Count: > 0 })
        {
            var (matchMap, p) = BuildPropertyMap(match, "match", nameof(match));
            query = $"MATCH (n:{label} {matchMap}) {deleteKeyword} n";
            parameters = p;
        }
        else
        {
            query = $"MATCH (n:{label}) {deleteKeyword} n";
        }

        using var result = await client.ExecuteAsync(query, parameters, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    // ── Merge (upsert) ─────────────────────────────────────────────────────────

    /// <summary>
    /// Merges (upserts) a node with the given <paramref name="label"/> and identity
    /// <paramref name="mergeProperties"/>, returning the merged node deserialized as
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// Generated Cypher (example):
    /// <code>MERGE (n:User {email: $merge_email}) RETURN n</code>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="mergeProperties"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">
    /// The database returned no rows. This indicates an unexpected database state because a
    /// <c>MERGE … RETURN n</c> query always returns the node on success.
    /// </exception>
    public static async ValueTask<T> MergeNodeAsync<T>(
        this ILoraDbClient client,
        string label,
        IReadOnlyDictionary<string, object?> mergeProperties,
        CancellationToken cancellationToken = default)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        ValidateLabel(label);
        if (mergeProperties is null) throw new ArgumentNullException(nameof(mergeProperties));
        if (mergeProperties.Count == 0) throw new ArgumentException("At least one merge property is required.", nameof(mergeProperties));

        var (propMap, parameters) = BuildPropertyMap(mergeProperties, "merge", nameof(mergeProperties));
        var query = $"MERGE (n:{label} {propMap}) RETURN n";

        using var result = await client.ExecuteAsync(query, parameters, cancellationToken: cancellationToken).ConfigureAwait(false);
        var rows = result.ReadRows<T>();
        if (rows.Count == 0)
            throw new InvalidOperationException("MERGE did not return any rows.");
        return rows[0];
    }

    // ── Batch ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="LoraDbBatch"/> bound to this client.
    /// Use the returned batch to queue multiple Cypher statements and execute them
    /// sequentially with <see cref="LoraDbBatch.ExecuteAsync"/>.
    /// </summary>
    public static LoraDbBatch CreateBatch(this ILoraDbClient client)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        return new LoraDbBatch(client);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Matches valid Cypher identifiers: start with a letter or underscore, followed by
    /// letters, digits, or underscores. This pattern is enforced for labels and property
    /// keys that are interpolated directly into generated Cypher.
    /// </summary>
    private static readonly Regex ValidIdentifier =
        new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static void ValidateLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label cannot be null or whitespace.", nameof(label));
        if (!ValidIdentifier.IsMatch(label))
            throw new ArgumentException(
                $"Label '{label}' is not a valid Cypher identifier. Use letters, digits, and underscores only, starting with a letter or underscore.",
                nameof(label));
    }

    private static void ValidatePropertyKey(string key, string paramName)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace.", paramName);
        if (!ValidIdentifier.IsMatch(key))
            throw new ArgumentException(
                $"Property key '{key}' is not a valid Cypher identifier. Use letters, digits, and underscores only, starting with a letter or underscore.",
                paramName);
    }

    /// <summary>
    /// Builds an inline Cypher property map <c>{key: $prefix_key, …}</c> and the
    /// corresponding parameter dictionary.
    /// </summary>
    private static (string propertyMap, Dictionary<string, object?> parameters) BuildPropertyMap(
        IReadOnlyDictionary<string, object?> properties, string prefix, string paramName)
    {
        var parts = new List<string>(properties.Count);
        var parameters = new Dictionary<string, object?>(properties.Count);
        foreach (var (key, value) in properties)
        {
            ValidatePropertyKey(key, paramName);
            var pName = $"{prefix}_{key}";
            parts.Add($"{key}: ${pName}");
            parameters[pName] = value;
        }
        return ($"{{{string.Join(", ", parts)}}}", parameters);
    }

    /// <summary>
    /// Builds a Cypher SET clause <c>alias.key = $prefix_key, …</c> and the
    /// corresponding parameter dictionary.
    /// </summary>
    private static (string setClause, Dictionary<string, object?> parameters) BuildSetClause(
        IReadOnlyDictionary<string, object?> properties, string nodeAlias, string prefix, string paramName)
    {
        var sb = new StringBuilder();
        var parameters = new Dictionary<string, object?>(properties.Count);
        var first = true;
        foreach (var (key, value) in properties)
        {
            ValidatePropertyKey(key, paramName);
            if (!first) sb.Append(", ");
            first = false;
            var pName = $"{prefix}_{key}";
            sb.Append($"{nodeAlias}.{key} = ${pName}");
            parameters[pName] = value;
        }
        return (sb.ToString(), parameters);
    }
}
