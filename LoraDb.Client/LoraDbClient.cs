using LoraDb.Client.Native;
using LoraDb.Client.Transports;

namespace LoraDb.Client;

public sealed class LoraDbClient : IAsyncDisposable
{
    private readonly ILoraDbTransport _transport;

    private LoraDbClientMode Mode { get; }

    private LoraDbClient(LoraDbClientMode mode, ILoraDbTransport transport)
    {
        Mode = mode;
        _transport = transport;
    }

    public static LoraDbClient CreateHttp(Uri endpoint, HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return new LoraDbClient(LoraDbClientMode.Http, new HttpLoraDbTransport(endpoint, httpClient));
    }

    public static LoraDbClient CreateEmbedded(ILoraDbNativeBridge? nativeBridge = null)
    {
        return new LoraDbClient(LoraDbClientMode.Embedded, new EmbeddedLoraDbTransport(nativeBridge ?? new PInvokeLoraDbNativeBridge()));
    }

    public Task<LoraDbQueryResult> ExecuteAsync(string query, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be null or whitespace.", nameof(query));
        }

        return _transport.ExecuteAsync(query, parameters, cancellationToken);
    }

    public ValueTask DisposeAsync() => _transport.DisposeAsync();
}
