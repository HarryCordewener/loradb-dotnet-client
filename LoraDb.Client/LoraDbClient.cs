using System.Text.Json;
using LoraDb.Client.Native;
using LoraDb.Client.Transports;

namespace LoraDb.Client;

public sealed class LoraDbClient : ILoraDbClient
{
    private readonly ILoraDbTransport _transport;

    private LoraDbClient(ILoraDbTransport transport)
    {
        _transport = transport;
    }

    public static LoraDbClient CreateHttp(Uri endpoint, HttpClient? httpClient = null, JsonSerializerOptions? serializerOptions = null)
    {
        if (endpoint is null)
            throw new ArgumentNullException(nameof(endpoint));
        return new LoraDbClient(new HttpLoraDbTransport(endpoint, httpClient, serializerOptions));
    }

    /// <summary>
    /// Creates an HTTP-mode client using an <see cref="IHttpClientFactory"/>.
    /// Prefer this overload when the factory is available (e.g. from DI) so the
    /// underlying <see cref="HttpClient"/> lifetime is managed correctly.
    /// </summary>
    public static LoraDbClient CreateHttp(
        Uri endpoint,
        IHttpClientFactory httpClientFactory,
        string clientName = nameof(LoraDbClient),
        JsonSerializerOptions? serializerOptions = null)
    {
        if (endpoint is null)
            throw new ArgumentNullException(nameof(endpoint));
        if (httpClientFactory is null)
            throw new ArgumentNullException(nameof(httpClientFactory));
        return new LoraDbClient(new HttpLoraDbTransport(endpoint, httpClientFactory, clientName, serializerOptions));
    }

    public static LoraDbClient CreateEmbedded(ILoraDbNativeBridge? nativeBridge = null, JsonSerializerOptions? serializerOptions = null)
    {
        return new LoraDbClient(new EmbeddedLoraDbTransport(nativeBridge ?? new PInvokeLoraDbNativeBridge(), serializerOptions));
    }

    public Task<LoraDbQueryResult> ExecuteAsync(string query, IReadOnlyDictionary<string, object?>? parameters = null, string format = Models.LoraDbQueryRequest.DefaultFormat, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be null or whitespace.", nameof(query));
        }

        return _transport.ExecuteAsync(query, parameters, format, cancellationToken);
    }

    public ValueTask DisposeAsync() => _transport.DisposeAsync();
}
