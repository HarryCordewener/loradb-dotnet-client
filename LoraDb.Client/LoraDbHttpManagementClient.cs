using System.Text.Json;
using LoraDb.Client.Models;
using LoraDb.Client.Transports;

namespace LoraDb.Client;

/// <summary>
/// An HTTP-mode LoraDB client that exposes both the standard query API and the
/// HTTP management surface (<c>/health</c>, <c>/explain</c>, <c>/profile</c>, and
/// the <c>/admin/*</c> endpoints).
/// </summary>
/// <remarks>
/// Admin endpoints are opt-in on the server side; see
/// <see cref="ILoraDbHttpManagementClient"/> for details.
/// </remarks>
public sealed class LoraDbHttpManagementClient : ILoraDbHttpManagementClient, ILoraDbCapabilitiesProvider
{
    private readonly HttpLoraDbTransport _transport;

    private LoraDbHttpManagementClient(HttpLoraDbTransport transport)
    {
        _transport = transport;
    }

    // ── Factory methods ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an HTTP management client that connects to the given
    /// <paramref name="endpoint"/>.
    /// </summary>
    /// <param name="endpoint">Base URI of the <c>lora-server</c> instance.</param>
    /// <param name="httpClient">
    /// Optional <see cref="HttpClient"/> to use.  When <c>null</c> a new client is
    /// created and disposed with this instance.  When provided, the caller retains
    /// ownership and the <see cref="HttpClient"/> will not be disposed.
    /// </param>
    /// <param name="serializerOptions">
    /// Optional custom <see cref="JsonSerializerOptions"/> applied when
    /// deserializing query results and management responses.
    /// </param>
    public static LoraDbHttpManagementClient Create(
        Uri endpoint,
        HttpClient? httpClient = null,
        JsonSerializerOptions? serializerOptions = null)
    {
        if (endpoint is null)
            throw new ArgumentNullException(nameof(endpoint));
        return new LoraDbHttpManagementClient(new HttpLoraDbTransport(endpoint, httpClient, serializerOptions));
    }

    /// <summary>
    /// Creates an HTTP management client using an <see cref="IHttpClientFactory"/>.
    /// Prefer this overload when the factory is available (e.g. from DI) so the
    /// underlying <see cref="HttpClient"/> lifetime is managed correctly.
    /// </summary>
    /// <param name="endpoint">Base URI of the <c>lora-server</c> instance.</param>
    /// <param name="httpClientFactory">Factory used to create the <see cref="HttpClient"/>.</param>
    /// <param name="clientName">Named-client key passed to the factory.</param>
    /// <param name="serializerOptions">
    /// Optional custom <see cref="JsonSerializerOptions"/> applied when
    /// deserializing query results and management responses.
    /// </param>
    public static LoraDbHttpManagementClient Create(
        Uri endpoint,
        IHttpClientFactory httpClientFactory,
        string clientName = nameof(LoraDbHttpManagementClient),
        JsonSerializerOptions? serializerOptions = null)
    {
        if (endpoint is null)
            throw new ArgumentNullException(nameof(endpoint));
        if (httpClientFactory is null)
            throw new ArgumentNullException(nameof(httpClientFactory));
        return new LoraDbHttpManagementClient(new HttpLoraDbTransport(endpoint, httpClientFactory, clientName, serializerOptions));
    }

    // ── ILoraDbClient ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task<LoraDbQueryResult> ExecuteAsync(
        string query,
        IReadOnlyDictionary<string, object?>? parameters = null,
        string format = Models.LoraDbQueryRequest.DefaultFormat,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be null or whitespace.", nameof(query));

        return _transport.ExecuteAsync(query, parameters, format, cancellationToken);
    }

    // ── ILoraDbHttpManagementClient ────────────────────────────────────────────

    public LoraDbClientCapabilities Capabilities => _transport.Capabilities;

    /// <inheritdoc/>
    public Task<LoraDbHealthResult> HealthAsync(CancellationToken cancellationToken = default)
        => _transport.HealthAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<LoraDbQueryPlan> ExplainAsync(
        string query,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be null or whitespace.", nameof(query));

        return _transport.ExplainAsync(query, parameters, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LoraDbQueryProfile> ProfileAsync(
        string query,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be null or whitespace.", nameof(query));

        return _transport.ProfileAsync(query, parameters, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LoraDbSnapshotMeta> SaveSnapshotAsync(
        string? path = null,
        CancellationToken cancellationToken = default)
        => _transport.SaveSnapshotAsync(path, cancellationToken);

    /// <inheritdoc/>
    public Task<LoraDbSnapshotMeta> LoadSnapshotAsync(
        string? path = null,
        CancellationToken cancellationToken = default)
        => _transport.LoadSnapshotAsync(path, cancellationToken);

    /// <inheritdoc/>
    public Task<LoraDbSnapshotMeta> CheckpointAsync(
        string? path = null,
        CancellationToken cancellationToken = default)
        => _transport.CheckpointAsync(path, cancellationToken);

    /// <inheritdoc/>
    public Task<LoraDbWalStatus> WalStatusAsync(CancellationToken cancellationToken = default)
        => _transport.WalStatusAsync(cancellationToken);

    /// <inheritdoc/>
    public Task TruncateWalAsync(
        long? fenceLsn = null,
        CancellationToken cancellationToken = default)
        => _transport.TruncateWalAsync(fenceLsn, cancellationToken);

    // ── IAsyncDisposable ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _transport.DisposeAsync();
}
