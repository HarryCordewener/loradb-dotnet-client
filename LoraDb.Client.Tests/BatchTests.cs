using System.Text.Json;
using LoraDb.Client.Tests.Helpers;
using TUnit.Assertions.Extensions;

namespace LoraDb.Client.Tests;

/// <summary>
/// Unit tests for <see cref="LoraDbBatch"/> and <see cref="LoraDbBatchResult"/>.
/// </summary>
public class BatchTests
{
    private static FakeNativeBridge EmptyBridge() => new("""{"rows":[]}""");

    // ── Basic fluent API ───────────────────────────────────────────────────────

    [Test]
    public async Task Add_ReturnsThisForChaining()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var batch = new LoraDbBatch(client);
        var returned = batch.Add("MATCH (n) RETURN n");

        await Assert.That(object.ReferenceEquals(batch, returned)).IsTrue();
    }

    [Test]
    public async Task Add_IncreasesCount()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var batch = new LoraDbBatch(client);
        batch.Add("MATCH (a) RETURN a").Add("MATCH (b) RETURN b");

        await Assert.That(batch.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Add_NullOrWhitespaceQuery_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var batch = new LoraDbBatch(client);

        await Assert.That(() => batch.Add("  "))
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
    }

    // ── Execute ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_EmptyBatch_ReturnsEmptyResultList()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var batch = new LoraDbBatch(client);
        using var batchResult = await batch.ExecuteAsync();

        await Assert.That(batchResult.Results.Count).IsEqualTo(0);
        await Assert.That(bridge.CallCount).IsEqualTo(0);
    }

    [Test]
    public async Task Execute_TwoStatements_CallsBridgeTwice()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var batch = new LoraDbBatch(client)
            .Add("MATCH (a) RETURN a")
            .Add("MATCH (b) RETURN b");

        using var batchResult = await batch.ExecuteAsync();

        await Assert.That(bridge.CallCount).IsEqualTo(2);
        await Assert.That(batchResult.Results.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Execute_ExecutesInOrder()
    {
        var callLog = new List<string>();
        var calls = 0;

        var bridge = new FakeNativeBridge(responseFactory: _ =>
        {
            calls++;
            callLog.Add($"call{calls}");
            return """{"rows":[]}""";
        });

        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var batch = new LoraDbBatch(client)
            .Add("MATCH (a) RETURN a")
            .Add("MATCH (b) RETURN b")
            .Add("MATCH (c) RETURN c");

        using var batchResult = await batch.ExecuteAsync();

        await Assert.That(batchResult.Results.Count).IsEqualTo(3);
        await Assert.That(calls).IsEqualTo(3);
    }

    [Test]
    public async Task Execute_ResultsContainCorrectJsonPayloads()
    {
        var responses = new Queue<string>(new[]
        {
            """{"rows":[{"total":1}]}""",
            """{"rows":[{"total":2}]}""",
        });

        var bridge = new FakeNativeBridge(responseFactory: _ => responses.Dequeue());
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var batch = new LoraDbBatch(client)
            .Add("RETURN 1 AS total")
            .Add("RETURN 2 AS total");

        using var batchResult = await batch.ExecuteAsync();

        var first = batchResult.Results[0].Root.GetProperty("rows")[0].GetProperty("total").GetInt32();
        var second = batchResult.Results[1].Root.GetProperty("rows")[0].GetProperty("total").GetInt32();
        await Assert.That(first).IsEqualTo(1);
        await Assert.That(second).IsEqualTo(2);
    }

    [Test]
    public async Task Execute_PassesParametersToEachStatement()
    {
        var capturedRequests = new List<string>();
        var bridge = new FakeNativeBridge(responseFactory: req =>
        {
            capturedRequests.Add(req);
            return """{"rows":[]}""";
        });
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var batch = new LoraDbBatch(client)
            .Add("MATCH (n) WHERE n.id = $id RETURN n",
                new Dictionary<string, object?> { ["id"] = "a1" })
            .Add("MATCH (n) WHERE n.id = $id RETURN n",
                new Dictionary<string, object?> { ["id"] = "b2" });

        using var batchResult = await batch.ExecuteAsync();

        await Assert.That(capturedRequests.Count).IsEqualTo(2);
        using var doc1 = JsonDocument.Parse(capturedRequests[0]);
        using var doc2 = JsonDocument.Parse(capturedRequests[1]);
        await Assert.That(doc1.RootElement.GetProperty("params").GetProperty("id").GetString()).IsEqualTo("a1");
        await Assert.That(doc2.RootElement.GetProperty("params").GetProperty("id").GetString()).IsEqualTo("b2");
    }

    // ── Fail-fast ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_FirstStatementFails_DoesNotExecuteSubsequentStatements()
    {
        var callCount = 0;
        var bridge = new FakeNativeBridge(responseFactory: req =>
        {
            callCount++;
            if (callCount == 1) throw new InvalidOperationException("Simulated DB error");
            return """{"rows":[]}""";
        });
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var batch = new LoraDbBatch(client)
            .Add("FAILING QUERY")
            .Add("MATCH (n) RETURN n");

        await Assert.That(async () => await batch.ExecuteAsync())
            .ThrowsException()
            .And.IsTypeOf<InvalidOperationException>();

        await Assert.That(callCount).IsEqualTo(1);
    }

    [Test]
    public async Task Execute_SecondStatementFails_FirstResultDisposed()
    {
        var callCount = 0;
        var bridge = new FakeNativeBridge(responseFactory: req =>
        {
            callCount++;
            if (callCount == 2) throw new InvalidOperationException("Simulated DB error on second call");
            return """{"rows":[]}""";
        });
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var batch = new LoraDbBatch(client)
            .Add("MATCH (a) RETURN a")
            .Add("FAILING QUERY")
            .Add("MATCH (c) RETURN c");

        await Assert.That(async () => await batch.ExecuteAsync())
            .ThrowsException()
            .And.IsTypeOf<InvalidOperationException>();

        // Only two statements attempted: the first succeeded, the second failed
        await Assert.That(callCount).IsEqualTo(2);
    }

    // ── LoraDbBatchResult disposal ─────────────────────────────────────────────

    [Test]
    public async Task BatchResult_Dispose_CanBeCalledMultipleTimes()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var batch = new LoraDbBatch(client).Add("MATCH (n) RETURN n");
        var batchResult = await batch.ExecuteAsync();

        // Should not throw even when called twice
        batchResult.Dispose();
        batchResult.Dispose();
    }

    [Test]
    public async Task BatchResult_ResultsAreAccessibleAfterExecution()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var batch = new LoraDbBatch(client).Add("MATCH (n) RETURN n");
        using var batchResult = await batch.ExecuteAsync();

        await Assert.That(batchResult.Results.Count).IsEqualTo(1);
        await Assert.That(batchResult.Results[0].Root.GetProperty("rows").GetArrayLength()).IsEqualTo(0);
    }

    // ── Constructor guards ─────────────────────────────────────────────────────

    [Test]
    public async Task Constructor_NullClient_ThrowsArgumentNullException()
    {
        await Assert.That(() => new LoraDbBatch(null!))
            .ThrowsException()
            .And.IsTypeOf<ArgumentNullException>();
    }
}
