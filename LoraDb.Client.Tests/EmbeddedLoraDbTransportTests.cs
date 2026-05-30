using System.Text.Json;
using LoraDb.Client.Native;
using LoraDb.Client.Tests.Helpers;
using TUnit.Assertions.Extensions;

namespace LoraDb.Client.Tests;

public class EmbeddedLoraDbTransportTests
{
    [Test]
    public async Task ExecuteAsync_InvokesNativeBridgeWithQueryJson()
    {
        var bridge = new FakeNativeBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("MATCH (u:User) RETURN u.name AS name");

        await Assert.That(bridge.LastRequestJson).IsNotNull();
        using var doc = JsonDocument.Parse(bridge.LastRequestJson!);
        await Assert.That(doc.RootElement.GetProperty("query").GetString())
            .IsEqualTo("MATCH (u:User) RETURN u.name AS name");
    }

    [Test]
    public async Task ExecuteAsync_SendsParametersToNativeBridge()
    {
        var bridge = new FakeNativeBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync(
            "MATCH (u:User) RETURN u.name AS name",
            new Dictionary<string, object?> { ["limit"] = 1 });

        using var doc = JsonDocument.Parse(bridge.LastRequestJson!);
        await Assert.That(doc.RootElement.GetProperty("params").GetProperty("limit").GetInt32())
            .IsEqualTo(1);
    }

    [Test]
    public async Task ExecuteAsync_ReturnsRowsFromNativeBridgeResponse()
    {
        var bridge = new FakeNativeBridge("""{"rows":[{"name":"Alice"}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("MATCH (u:User) RETURN u.name AS name");

        await Assert.That(result.Root.GetProperty("rows")[0].GetProperty("name").GetString())
            .IsEqualTo("Alice");
    }

    [Test]
    public async Task ExecuteAsync_RejectsEmptyQuery()
    {
        var bridge = new FakeNativeBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var ex = await Assert.That(async () => await client.ExecuteAsync("  "))
            .ThrowsException()
            .And
            .IsTypeOf<ArgumentException>();

        await Assert.That(ex!.ParamName).IsEqualTo("query");
    }

    [Test]
    public async Task ExecuteAsync_DoesNotCallBridgeWhenCancelled()
    {
        var bridge = new FakeNativeBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.That(async () => await client.ExecuteAsync("MATCH (n) RETURN n", cancellationToken: cts.Token))
            .ThrowsException()
            .And
            .IsTypeOf<OperationCanceledException>();

        await Assert.That(bridge.CallCount).IsEqualTo(0);
    }

    [Test]
    public async Task ExecuteAsync_MultipleCallsAreIndependent()
    {
        var bridge = new FakeNativeBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var r1 = await client.ExecuteAsync("MATCH (a:User) RETURN a");
        using var r2 = await client.ExecuteAsync("MATCH (b:Order) RETURN b");

        await Assert.That(bridge.CallCount).IsEqualTo(2);
    }

    [Test]
    public async Task DisposeAsync_DisposesNativeBridge()
    {
        var bridge = new TrackingDisposeBridge();
        var client = LoraDbClient.CreateEmbedded(bridge);

        await client.DisposeAsync();

        await Assert.That(bridge.Disposed).IsTrue();
    }

    private sealed class TrackingDisposeBridge : ILoraDbNativeBridge
    {
        public bool Disposed { get; private set; }

        public string ExecuteJson(string requestJson) => """{"rows":[]}""";

        public void Dispose() => Disposed = true;
    }
}
