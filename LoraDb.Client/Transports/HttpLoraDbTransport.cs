using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    // ── Management / admin endpoints ───────────────────────────────────────────

    /// <inheritdoc cref="ILoraDbHttpManagementClient.HealthAsync"/>
    public async Task<LoraDbHealthResult> HealthAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync("health", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await DeserializeResponseAsync<LoraDbHealthResult>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ILoraDbHttpManagementClient.ExplainAsync"/>
    public async Task<LoraDbQueryPlan> ExplainAsync(
        string query,
        IReadOnlyDictionary<string, object?>? parameters,
        CancellationToken cancellationToken)
    {
        var request = new AnalysisRequest(query) { Parameters = parameters };
        using var response = await _httpClient
            .PostAsJsonAsync("explain", request, LoraDbJsonSerializerOptions.RequestSerializationOptions, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await DeserializeResponseAsync<LoraDbQueryPlan>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ILoraDbHttpManagementClient.ProfileAsync"/>
    public async Task<LoraDbQueryProfile> ProfileAsync(
        string query,
        IReadOnlyDictionary<string, object?>? parameters,
        CancellationToken cancellationToken)
    {
        var request = new AnalysisRequest(query) { Parameters = parameters };
        using var response = await _httpClient
            .PostAsJsonAsync("profile", request, LoraDbJsonSerializerOptions.RequestSerializationOptions, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await DeserializeResponseAsync<LoraDbQueryProfile>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ILoraDbHttpManagementClient.SaveSnapshotAsync"/>
    public Task<LoraDbSnapshotMeta> SaveSnapshotAsync(string? path, CancellationToken cancellationToken)
        => PostSnapshotAdminAsync("admin/snapshot/save", path, cancellationToken);

    /// <inheritdoc cref="ILoraDbHttpManagementClient.LoadSnapshotAsync"/>
    public Task<LoraDbSnapshotMeta> LoadSnapshotAsync(string? path, CancellationToken cancellationToken)
        => PostSnapshotAdminAsync("admin/snapshot/load", path, cancellationToken);

    /// <inheritdoc cref="ILoraDbHttpManagementClient.CheckpointAsync"/>
    public Task<LoraDbSnapshotMeta> CheckpointAsync(string? path, CancellationToken cancellationToken)
        => PostSnapshotAdminAsync("admin/checkpoint", path, cancellationToken);

    private async Task<LoraDbSnapshotMeta> PostSnapshotAdminAsync(
        string relativeUri,
        string? path,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        if (path is null)
        {
            response = await _httpClient.PostAsync(relativeUri, null, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var body = new PathRequest { Path = path };
            response = await _httpClient
                .PostAsJsonAsync(relativeUri, body, LoraDbJsonSerializerOptions.RequestSerializationOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        using (response)
        {
            response.EnsureSuccessStatusCode();
            return await DeserializeResponseAsync<LoraDbSnapshotMeta>(response, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc cref="ILoraDbHttpManagementClient.WalStatusAsync"/>
    public async Task<LoraDbWalStatus> WalStatusAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync("admin/wal/status", null, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await DeserializeResponseAsync<LoraDbWalStatus>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ILoraDbHttpManagementClient.TruncateWalAsync"/>
    public async Task TruncateWalAsync(long? fenceLsn, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        if (fenceLsn is null)
        {
            response = await _httpClient.PostAsync("admin/wal/truncate", null, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var body = new WalTruncateRequest { FenceLsn = fenceLsn };
            response = await _httpClient
                .PostAsJsonAsync("admin/wal/truncate", body, LoraDbJsonSerializerOptions.RequestSerializationOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        using (response)
        {
            response.EnsureSuccessStatusCode(); // 204 No Content on success
        }
    }

    private async Task<T> DeserializeResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
#if NETSTANDARD2_1
        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#else
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#endif
        var result = await JsonSerializer.DeserializeAsync<T>(stream, _resultSerializerOptions, cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException($"Server returned a null {typeof(T).Name} response.");
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        return default;
    }

    // ── Private request types ──────────────────────────────────────────────────

    private sealed class AnalysisRequest
    {
        public AnalysisRequest(string query) => Query = query;

        [JsonPropertyName("query")]
        public string Query { get; }

        [JsonPropertyName("params")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyDictionary<string, object?>? Parameters { get; set; }
    }

    private sealed class PathRequest
    {
        [JsonPropertyName("path")]
        public string? Path { get; set; }
    }

    private sealed class WalTruncateRequest
    {
        [JsonPropertyName("fenceLsn")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? FenceLsn { get; set; }
    }
}
