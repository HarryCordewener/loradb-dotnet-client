using System.Text.Json;
using LoraDb.Client.Tests.Helpers;
using TUnit.Assertions.Extensions;

namespace LoraDb.Client.Tests;

public class ErrorHandlingTests
{
    private static readonly Uri Endpoint = new("http://localhost:4747/");

    [Test]
    public async Task Http_InvalidCypher_400_ThrowsHttpRequestException()
    {
        var handler = RecordingHttpHandler.WithStatus(
            System.Net.HttpStatusCode.BadRequest,
            """{"error":{"code":"LORA_PARSE","category":"client","message":"unexpected token 'IS'"}}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        var ex = await Assert.That(async () => await client.ExecuteAsync("THIS IS NOT CYPHER"))
            .ThrowsException()
            .And
            .IsTypeOf<HttpRequestException>();

        await Assert.That(ex!.Message).Contains("400");
    }

    [Test]
    public async Task Http_ServiceUnavailable_503_Throws()
    {
        var handler = RecordingHttpHandler.WithStatus(
            System.Net.HttpStatusCode.ServiceUnavailable,
            """{"error":{"code":"LORA_CONNECTION","category":"server","message":"temporarily unavailable"}}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        await Assert.That(async () => await client.ExecuteAsync("RETURN 1"))
            .ThrowsException()
            .And
            .IsTypeOf<HttpRequestException>();
    }

    [Test]
    public async Task Http_InternalServerError_500_Throws()
    {
        var handler = RecordingHttpHandler.WithStatus(
            System.Net.HttpStatusCode.InternalServerError,
            """{"error":{"code":"LORA_INTERNAL","category":"server","message":"database operation failed unexpectedly"}}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        await Assert.That(async () => await client.ExecuteAsync("RETURN 1"))
            .ThrowsException()
            .And
            .IsTypeOf<HttpRequestException>();
    }

    [Test]
    public async Task Http_NotFound_404_Throws()
    {
        var handler = RecordingHttpHandler.WithStatus(
            System.Net.HttpStatusCode.NotFound,
            """{"error":{"code":"LORA_NOT_FOUND","category":"client","message":"catalog entry not found"}}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        await Assert.That(async () => await client.ExecuteAsync("DROP CONSTRAINT missing"))
            .ThrowsException()
            .And
            .IsTypeOf<HttpRequestException>();
    }

    [Test]
    public async Task Http_Conflict_409_Throws()
    {
        var handler = RecordingHttpHandler.WithStatus(
            System.Net.HttpStatusCode.Conflict,
            """{"error":{"code":"LORA_UNIQUE_CONSTRAINT","category":"client","message":"uniqueness constraint violated"}}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        await Assert.That(async () => await client.ExecuteAsync("CREATE (u:User {id: 1}) RETURN u"))
            .ThrowsException()
            .And
            .IsTypeOf<HttpRequestException>();
    }

    [Test]
    public async Task Http_UnprocessableEntity_422_Throws()
    {
        var handler = RecordingHttpHandler.WithStatus(
            System.Net.HttpStatusCode.UnprocessableContent,
            """{"error":{"code":"LORA_INVALID_PARAMS","category":"client","message":"params must be an object"}}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        await Assert.That(async () => await client.ExecuteAsync("RETURN $v AS v"))
            .ThrowsException()
            .And
            .IsTypeOf<HttpRequestException>();
    }

    [Test]
    public async Task EmptyQuery_ThrowsArgumentException_BothTransports()
    {
        var bridge = new FakeNativeBridge();
        await using var embeddedClient = LoraDbClient.CreateEmbedded(bridge);

        var handler = RecordingHttpHandler.WithJson("""{"rows":[]}""");
        await using var httpClient = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        await Assert.That(async () => await embeddedClient.ExecuteAsync(""))
            .ThrowsException().And.IsTypeOf<ArgumentException>();

        await Assert.That(async () => await httpClient.ExecuteAsync(""))
            .ThrowsException().And.IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task WhitespaceOnlyQuery_ThrowsArgumentException()
    {
        var bridge = new FakeNativeBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () => await client.ExecuteAsync("   \t  "))
            .ThrowsException().And.IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task NullQuery_ThrowsArgumentException()
    {
        var bridge = new FakeNativeBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () => await client.ExecuteAsync(null!))
            .ThrowsException().And.IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task Embedded_NativeBridgeThrows_ExceptionPropagates()
    {
        var bridge = new ExplodingBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () => await client.ExecuteAsync("RETURN 1"))
            .ThrowsException()
            .And
            .IsTypeOf<InvalidOperationException>();
    }

    [Test]
    public async Task Embedded_BridgeReturnsInvalidJson_ThrowsJsonException()
    {
        var bridge = new FakeNativeBridge("not valid json");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () => await client.ExecuteAsync("MATCH (n) RETURN n"))
            .ThrowsException()
            .And
            .IsTypeOf<JsonException>();
    }

    [Test]
    public async Task CancelledToken_BeforeExecute_Throws_Embedded()
    {
        var bridge = new FakeNativeBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.That(async () => await client.ExecuteAsync("RETURN 1", cancellationToken: cts.Token))
            .ThrowsException()
            .And
            .IsTypeOf<OperationCanceledException>();
    }

    private sealed class ExplodingBridge : LoraDb.Client.Native.ILoraDbNativeBridge
    {
        public string ExecuteJson(string requestJson) =>
            throw new InvalidOperationException("Simulated native bridge failure.");

        public string ExplainJson(string requestJson) =>
            throw new InvalidOperationException("Simulated native bridge failure.");

        public string ProfileJson(string requestJson) =>
            throw new InvalidOperationException("Simulated native bridge failure.");

        public LoraDb.Client.Models.LoraDbSnapshotMeta SaveSnapshot(string path) =>
            throw new InvalidOperationException("Simulated native bridge failure.");

        public LoraDb.Client.Models.LoraDbSnapshotMeta LoadSnapshot(string path) =>
            throw new InvalidOperationException("Simulated native bridge failure.");

        public void Dispose() { }
    }
}
