using System.Text.Json;
using LoraDb.Client.Models;
using LoraDb.Client.Native;
using LoraDb.Client.Transports;

namespace LoraDb.Client;

public sealed class LoraDbEmbeddedManagementClient : ILoraDbEmbeddedManagementClient
{
    private readonly EmbeddedLoraDbTransport _transport;

    private LoraDbEmbeddedManagementClient(EmbeddedLoraDbTransport transport)
    {
        _transport = transport;
    }

    public static LoraDbEmbeddedManagementClient Create(
        ILoraDbNativeBridge? nativeBridge = null,
        JsonSerializerOptions? serializerOptions = null)
    {
        return new LoraDbEmbeddedManagementClient(
            new EmbeddedLoraDbTransport(nativeBridge ?? new PInvokeLoraDbNativeBridge(), serializerOptions));
    }

    public static LoraDbEmbeddedManagementClient Create(
        LoraDbEmbeddedOpenOptions openOptions,
        JsonSerializerOptions? serializerOptions = null)
    {
        if (openOptions is null)
            throw new ArgumentNullException(nameof(openOptions));

        return new LoraDbEmbeddedManagementClient(
            new EmbeddedLoraDbTransport(new PInvokeLoraDbNativeBridge(openOptions), serializerOptions));
    }

    public LoraDbClientCapabilities Capabilities => _transport.Capabilities;

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

    public Task<LoraDbQueryPlan> ExplainAsync(
        string query,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be null or whitespace.", nameof(query));

        return _transport.ExplainAsync(query, parameters, cancellationToken);
    }

    public Task<LoraDbQueryProfile> ProfileAsync(
        string query,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be null or whitespace.", nameof(query));

        return _transport.ProfileAsync(query, parameters, cancellationToken);
    }

    public Task<LoraDbSnapshotMeta> SaveSnapshotAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));

        return _transport.SaveSnapshotAsync(path, cancellationToken);
    }

    public Task<LoraDbSnapshotMeta> LoadSnapshotAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));

        return _transport.LoadSnapshotAsync(path, cancellationToken);
    }

    public ValueTask DisposeAsync() => _transport.DisposeAsync();
}
