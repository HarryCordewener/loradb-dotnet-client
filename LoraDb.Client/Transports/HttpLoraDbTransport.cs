using System.Net.Http.Json;
using System.Text.Json;
using LoraDb.Client.Models;

namespace LoraDb.Client.Transports;

public sealed class HttpLoraDbTransport : ILoraDbTransport
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public HttpLoraDbTransport(Uri endpoint, HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = endpoint;
        }
    }

    public async Task<LoraDbQueryResult> ExecuteAsync(string query, IReadOnlyDictionary<string, object?>? parameters, CancellationToken cancellationToken)
    {
        var request = new LoraDbQueryRequest
        {
            Query = query,
            Parameters = parameters,
        };

        using var response = await _httpClient.PostAsJsonAsync("query", request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new LoraDbQueryResult(document);
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
