using System.Text.Json;
using LoraDb.Client.Models;
using LoraDb.Client.Native;
using LoraDb.Client.Serialization;

namespace LoraDb.Client.Transports;

public sealed class EmbeddedLoraDbTransport : ILoraDbTransport
{
    private readonly ILoraDbNativeBridge _nativeBridge;
    private readonly JsonSerializerOptions _resultSerializerOptions;
    private static readonly LoraDbClientCapabilities EmbeddedCapabilities = new()
    {
        SupportedResultFormats = ["rows"],
        SupportsExplain = true,
        SupportsProfile = true,
        SupportsSnapshots = true,
        SupportsCheckpoint = false,
        SupportsWalStatus = false,
        SupportsWalTruncate = false,
    };

    public LoraDbClientCapabilities Capabilities => EmbeddedCapabilities;

    public EmbeddedLoraDbTransport(ILoraDbNativeBridge nativeBridge, JsonSerializerOptions? serializerOptions = null)
    {
        _nativeBridge = nativeBridge ?? throw new ArgumentNullException(nameof(nativeBridge));
        _resultSerializerOptions = LoraDbJsonSerializerOptions.CreateResultOptions(serializerOptions);
    }

    /// <summary>
    /// Executes the given Cypher query synchronously via the native bridge and wraps the
    /// result in a completed <see cref="Task{T}"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <paramref name="format"/> parameter is included in the serialized request JSON
    /// but the native <c>lora_db_execute_json</c> function does not expose a format
    /// selection — the embedded engine always returns the default <c>rows</c> payload.
    /// Callers that require a specific response format (e.g. <c>graph</c> or
    /// <c>rowArrays</c>) must use the HTTP transport.
    /// </para>
    /// <para>
    /// Any synchronous exception thrown by the native bridge or JSON parser is faulted
    /// into the returned <see cref="Task{T}"/> rather than being thrown directly, so
    /// callers that <c>await</c> this method always observe exceptions uniformly.
    /// </para>
    /// </remarks>
    public Task<LoraDbQueryResult> ExecuteAsync(string query, IReadOnlyDictionary<string, object?>? parameters, string format, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(format, LoraDbQueryRequest.DefaultFormat, StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException($"Embedded mode supports only '{LoraDbQueryRequest.DefaultFormat}' format. Requested '{format}'.");

            var request = new LoraDbQueryRequest(query)
            {
                Parameters = parameters,
                Format = format,
            };

            var requestJson = JsonSerializer.Serialize(request, LoraDbJsonSerializerOptions.RequestSerializationOptions);
            var responseJson = _nativeBridge.ExecuteJson(requestJson);
            var document = JsonDocument.Parse(responseJson);

            return Task.FromResult(new LoraDbQueryResult(document, _resultSerializerOptions));
        }
        catch (Exception ex)
        {
            return Task.FromException<LoraDbQueryResult>(ex);
        }
    }

    public Task<LoraDbQueryPlan> ExplainAsync(string query, IReadOnlyDictionary<string, object?>? parameters, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var requestJson = BuildRequestJson(query, parameters);
            var responseJson = _nativeBridge.ExplainJson(requestJson);
            var plan = JsonSerializer.Deserialize<LoraDbQueryPlan>(responseJson, _resultSerializerOptions);
            return plan is null
                ? Task.FromException<LoraDbQueryPlan>(new InvalidOperationException("Embedded explain returned a null payload."))
                : Task.FromResult(plan);
        }
        catch (Exception ex)
        {
            return Task.FromException<LoraDbQueryPlan>(ex);
        }
    }

    public Task<LoraDbQueryProfile> ProfileAsync(string query, IReadOnlyDictionary<string, object?>? parameters, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var requestJson = BuildRequestJson(query, parameters);
            var responseJson = _nativeBridge.ProfileJson(requestJson);
            var profile = JsonSerializer.Deserialize<LoraDbQueryProfile>(responseJson, _resultSerializerOptions);
            return profile is null
                ? Task.FromException<LoraDbQueryProfile>(new InvalidOperationException("Embedded profile returned a null payload."))
                : Task.FromResult(profile);
        }
        catch (Exception ex)
        {
            return Task.FromException<LoraDbQueryProfile>(ex);
        }
    }

    public Task<LoraDbSnapshotMeta> SaveSnapshotAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_nativeBridge.SaveSnapshot(path));
        }
        catch (Exception ex)
        {
            return Task.FromException<LoraDbSnapshotMeta>(ex);
        }
    }

    public Task<LoraDbSnapshotMeta> LoadSnapshotAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_nativeBridge.LoadSnapshot(path));
        }
        catch (Exception ex)
        {
            return Task.FromException<LoraDbSnapshotMeta>(ex);
        }
    }

    public ValueTask DisposeAsync()
    {
        _nativeBridge.Dispose();
        return default;
    }

    private static string BuildRequestJson(string query, IReadOnlyDictionary<string, object?>? parameters)
    {
        var request = new LoraDbQueryRequest(query)
        {
            Parameters = parameters,
            Format = LoraDbQueryRequest.DefaultFormat,
        };
        return JsonSerializer.Serialize(request, LoraDbJsonSerializerOptions.RequestSerializationOptions);
    }
}
