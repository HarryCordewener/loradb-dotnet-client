using System.Net;
using System.Text;
using System.Text.Json;
using LoraDb.Client.Tests.Helpers;
using TUnit.Assertions.Extensions;

namespace LoraDb.Client.Tests;

public class HttpLoraDbTransportTests
{
    private static readonly Uri Endpoint = new("http://localhost:4747/");

    [Test]
    public async Task ExecuteAsync_PostsToQueryPath()
    {
        var handler = RecordingHttpHandler.WithJson("""{"rows":[{"name":"Alice"}]}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync("MATCH (u:User) RETURN u.name AS name");

        await Assert.That(handler.LastRequest).IsNotNull();
        await Assert.That(handler.LastRequest!.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(handler.LastRequest.RequestUri!.AbsolutePath).IsEqualTo("/query");
    }

    [Test]
    public async Task ExecuteAsync_SendsQueryInBody()
    {
        var handler = RecordingHttpHandler.WithJson("""{"rows":[]}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync("MATCH (n:User) RETURN n");

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        await Assert.That(doc.RootElement.GetProperty("query").GetString())
            .IsEqualTo("MATCH (n:User) RETURN n");
    }

    [Test]
    public async Task ExecuteAsync_SendsParametersInBody()
    {
        var handler = RecordingHttpHandler.WithJson("""{"rows":[{"name":"Alice"}]}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync(
            "MATCH (u:User) WHERE u.name = $name RETURN u.name AS name",
            new Dictionary<string, object?> { ["name"] = "Alice" });

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        await Assert.That(doc.RootElement.GetProperty("params").GetProperty("name").GetString())
            .IsEqualTo("Alice");
    }

    [Test]
    public async Task ExecuteAsync_ReturnsRowsFromResponse()
    {
        var handler = RecordingHttpHandler.WithJson("""{"rows":[{"name":"Alice"}]}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync("MATCH (u:User) RETURN u.name AS name");

        await Assert.That(result.Root.GetProperty("rows")[0].GetProperty("name").GetString())
            .IsEqualTo("Alice");
    }

    [Test]
    public async Task ExecuteAsync_ThrowsOnNonSuccessStatus()
    {
        var handler = RecordingHttpHandler.WithStatus(
            HttpStatusCode.BadRequest,
            """{"error":{"code":"LORA_PARSE","message":"invalid syntax"}}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        await Assert.That(() => client.ExecuteAsync("THIS IS NOT CYPHER"))
            .ThrowsException()
            .WithMessageContaining("400");
    }

    [Test]
    public async Task ExecuteAsync_ThrowsOnServiceUnavailable()
    {
        var handler = RecordingHttpHandler.WithStatus(
            HttpStatusCode.ServiceUnavailable,
            """{"error":{"code":"LORA_CONNECTION","message":"temporarily unavailable"}}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        await Assert.That(() => client.ExecuteAsync("MATCH (n) RETURN n"))
            .ThrowsException()
            .WithMessageContaining("503");
    }

    [Test]
    public async Task ExecuteAsync_SendsDefaultRowsFormat()
    {
        var handler = RecordingHttpHandler.WithJson("""{"rows":[]}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync("MATCH (n) RETURN n");

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        await Assert.That(doc.RootElement.GetProperty("format").GetString()).IsEqualTo("rows");
    }

    [Test]
    public async Task ExecuteAsync_MultipleSequentialCallsSucceed()
    {
        var handler = RecordingHttpHandler.WithJson("""{"rows":[{"name":"Alice"}]}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var r1 = await client.ExecuteAsync("MATCH (u:User) RETURN u");
        using var r2 = await client.ExecuteAsync("MATCH (u:User) RETURN u");

        await Assert.That(handler.CallCount).IsEqualTo(2);
    }

    [Test]
    public async Task ExecuteAsync_WithNullParameters_OmitsParamsFromBody()
    {
        var handler = RecordingHttpHandler.WithJson("""{"rows":[]}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync("MATCH (n) RETURN n", null);

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        await Assert.That(doc.RootElement.TryGetProperty("params", out _)).IsFalse();
    }

    [Test]
    public async Task ExecuteAsync_WithIntegerParameter_SendsCorrectType()
    {
        var handler = RecordingHttpHandler.WithJson("""{"rows":[{"val":42}]}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync(
            "RETURN $v AS val",
            new Dictionary<string, object?> { ["v"] = 42 });

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        await Assert.That(doc.RootElement.GetProperty("params").GetProperty("v").GetInt32())
            .IsEqualTo(42);
    }

    [Test]
    public async Task ExecuteAsync_ThrowsForEmptyQuery()
    {
        var handler = RecordingHttpHandler.WithJson("""{"rows":[]}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        var ex = await Assert.That(() => client.ExecuteAsync("  "))
            .ThrowsException()
            .And
            .IsTypeOf<ArgumentException>();

        await Assert.That(ex.ParamName).IsEqualTo("query");
    }

    [Test]
    public async Task ExecuteAsync_ThrowsForWhitespaceOnlyQuery()
    {
        var handler = RecordingHttpHandler.WithJson("""{"rows":[]}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        await Assert.That(() => client.ExecuteAsync("\t\n"))
            .ThrowsException()
            .And
            .IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task CreateHttp_ThrowsForNullEndpoint()
    {
        await Assert.That(() => Task.FromResult(LoraDbClient.CreateHttp(null!)))
            .ThrowsException()
            .And
            .IsTypeOf<ArgumentNullException>();
    }

    // ── IHttpClientFactory overload ──────────────────────────────────

    [Test]
    public async Task CreateHttp_WithFactory_ExecutesQuery()
    {
        var handler = RecordingHttpHandler.WithJson("""{"rows":[{"name":"Alice"}]}""");
        var factory = new FakeHttpClientFactory(handler.BuildClient(Endpoint));
        await using var client = LoraDbClient.CreateHttp(Endpoint, factory);

        using var result = await client.ExecuteAsync("MATCH (u:User) RETURN u.name AS name");

        await Assert.That(handler.CallCount).IsEqualTo(1);
        await Assert.That(result.Root.GetProperty("rows")[0].GetProperty("name").GetString())
            .IsEqualTo("Alice");
    }

    [Test]
    public async Task CreateHttp_WithFactory_UsesNamedClient()
    {
        var handler = RecordingHttpHandler.WithJson("""{"rows":[]}""");
        string? capturedName = null;
        var factory = new SpyHttpClientFactory(handler.BuildClient(Endpoint), n => capturedName = n);
        await using var client = LoraDbClient.CreateHttp(Endpoint, factory, "my-client");

        using var _ = await client.ExecuteAsync("MATCH (n) RETURN n");

        await Assert.That(capturedName).IsEqualTo("my-client");
    }

    [Test]
    public async Task CreateHttp_WithFactory_DoesNotDisposeClient()
    {
        var handler = RecordingHttpHandler.WithJson("""{"rows":[]}""");
        var httpClient = handler.BuildClient(Endpoint);
        var factory = new FakeHttpClientFactory(httpClient);
        var transport = LoraDbClient.CreateHttp(Endpoint, factory);

        await transport.DisposeAsync();

        // The HttpClient should still be usable after the transport is disposed;
        // the factory owns the client lifetime, not the transport.
        // If the transport incorrectly disposed the client this would throw ObjectDisposedException.
        var response = await httpClient.GetAsync("/");
        await Assert.That((int)response.StatusCode).IsEqualTo(200);
    }

    [Test]
    public async Task CreateHttp_WithNullFactory_ThrowsArgumentNullException()
    {
        await Assert.That(() => Task.FromResult(LoraDbClient.CreateHttp(Endpoint, (IHttpClientFactory)null!)))
            .ThrowsException()
            .And
            .IsTypeOf<ArgumentNullException>();
    }

    [Test]
    public async Task CreateHttp_WithFactoryAndNullEndpoint_ThrowsArgumentNullException()
    {
        var factory = new FakeHttpClientFactory(new HttpClient());
        await Assert.That(() => Task.FromResult(LoraDbClient.CreateHttp(null!, factory)))
            .ThrowsException()
            .And
            .IsTypeOf<ArgumentNullException>();
    }

    // ── helpers ──────────────────────────────────────────────────────

    private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class SpyHttpClientFactory(HttpClient client, Action<string> onCreateClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            onCreateClient(name);
            return client;
        }
    }
}

