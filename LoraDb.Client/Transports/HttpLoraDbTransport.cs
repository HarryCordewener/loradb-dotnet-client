using System.Net.Http.Json;
using System.Text.Json;
using LoraDb.Client.Models;
using LoraDb.Client.Serialization;

namespace LoraDb.Client.Transports;

public sealed class HttpLoraDbTransport : ILoraDbTransport
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly JsonSerializerOptions _resultSerializerOptions;

    public HttpLoraDbTransport(Uri endpoint, HttpClient? httpClient = null, JsonSerializerOptions? serializerOptions = null)
    {
        if (endpoint is null)
            throw new ArgumentNullException(nameof(endpoint));

        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
        _resultSerializerOptions = LoraDbJsonSerializerOptions.CreateResultOptions(serializerOptions);

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = endpoint;
        }
    }

    /// <summary>
    /// Initialises the transport using an <see cref="IHttpClientFactory"/>.
    /// The factory manages the <see cref="HttpClient"/> lifetime; this transport
    /// will never dispose the client it receives.
    /// </summary>
    public HttpLoraDbTransport(
        Uri endpoint,
        IHttpClientFactory httpClientFactory,
        string clientName = nameof(HttpLoraDbTransport),
        JsonSerializerOptions? serializerOptions = null)
    {
        if (endpoint is null)
            throw new ArgumentNullException(nameof(endpoint));
        if (httpClientFactory is null)
            throw new ArgumentNullException(nameof(httpClientFactory));

        _ownsHttpClient = false; // lifetime is managed by the factory
        _httpClient = httpClientFactory.CreateClient(clientName);
        _resultSerializerOptions = LoraDbJsonSerializerOptions.CreateResultOptions(serializerOptions);

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = endpoint;
        }
    }

    public async Task<LoraDbQueryResult> ExecuteAsync(string query, IReadOnlyDictionary<string, object?>? parameters, string format, CancellationToken cancellationToken)
    {
        var request = new LoraDbQueryRequest(query)
        {
            Parameters = parameters,
            Format = format,
        };

        using var response = await _httpClient.PostAsJsonAsync("query", request, LoraDbJsonSerializerOptions.RequestSerializationOptions, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

#if NETSTANDARD2_1
        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#else
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#endif
        var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new LoraDbQueryResult(document, _resultSerializerOptions);
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        return default;
    }
}
