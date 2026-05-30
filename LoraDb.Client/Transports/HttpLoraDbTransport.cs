using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using LoraDb.Client.Models;

namespace LoraDb.Client.Transports;

public sealed class HttpLoraDbTransport : ILoraDbTransport
{
    // Use relaxed JSON escaping so Cypher syntax characters like `>` (e.g. `->`)
    // are serialized as-is rather than as \u003e. This JSON is only sent over a
    // trusted internal/local socket to the LoraDB server and is never rendered in
    // an HTML context, so XSS concerns do not apply.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public HttpLoraDbTransport(Uri endpoint, HttpClient? httpClient = null)
    {
        if (endpoint is null)
            throw new ArgumentNullException(nameof(endpoint));

        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();

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
    public HttpLoraDbTransport(Uri endpoint, IHttpClientFactory httpClientFactory, string clientName = nameof(HttpLoraDbTransport))
    {
        if (endpoint is null)
            throw new ArgumentNullException(nameof(endpoint));
        if (httpClientFactory is null)
            throw new ArgumentNullException(nameof(httpClientFactory));

        _ownsHttpClient = false; // lifetime is managed by the factory
        _httpClient = httpClientFactory.CreateClient(clientName);

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

        using var response = await _httpClient.PostAsJsonAsync("query", request, SerializerOptions, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new LoraDbQueryResult(document);
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
