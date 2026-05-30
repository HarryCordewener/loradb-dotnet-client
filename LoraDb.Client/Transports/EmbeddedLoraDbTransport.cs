using System.Text.Json;
using LoraDb.Client.Models;
using LoraDb.Client.Native;

namespace LoraDb.Client.Transports;

public sealed class EmbeddedLoraDbTransport : ILoraDbTransport
{
    private readonly ILoraDbNativeBridge _nativeBridge;

    public EmbeddedLoraDbTransport(ILoraDbNativeBridge nativeBridge)
    {
        _nativeBridge = nativeBridge ?? throw new ArgumentNullException(nameof(nativeBridge));
    }

    public Task<LoraDbQueryResult> ExecuteAsync(string query, IReadOnlyDictionary<string, object?>? parameters, string format, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = new LoraDbQueryRequest(query)
        {
            Parameters = parameters,
            Format = format,
        };

        var requestJson = JsonSerializer.Serialize(request);
        var responseJson = _nativeBridge.ExecuteJson(requestJson);
        var document = JsonDocument.Parse(responseJson);

        return Task.FromResult(new LoraDbQueryResult(document));
    }

    public ValueTask DisposeAsync()
    {
        _nativeBridge.Dispose();
        return default;
    }
}
