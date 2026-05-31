using System.Text.Json;
using LoraDb.Client.Models;
using LoraDb.Client.Native;
using LoraDb.Client.Serialization;

namespace LoraDb.Client.Transports;

public sealed class EmbeddedLoraDbTransport : ILoraDbTransport
{
    private readonly ILoraDbNativeBridge _nativeBridge;
    private readonly JsonSerializerOptions _resultSerializerOptions;

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

    public ValueTask DisposeAsync()
    {
        _nativeBridge.Dispose();
        return default;
    }
}
