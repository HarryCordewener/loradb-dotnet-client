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

    public Task<LoraDbQueryResult> ExecuteAsync(string query, IReadOnlyDictionary<string, object?>? parameters, string format, CancellationToken cancellationToken)
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

    public ValueTask DisposeAsync()
    {
        _nativeBridge.Dispose();
        return default;
    }
}
