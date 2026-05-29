using LoraDb.Client.Tests.Helpers;
using System.Text.Json;
using TUnit.Assertions.Extensions;

namespace LoraDb.Client.Tests;

/// <summary>
/// Tests lifted from lora-database/tests/update.rs and lora-database/tests/delete.rs.
/// Validates that SET, REMOVE, DELETE, DETACH DELETE, and MERGE Cypher clauses are
/// transmitted correctly via both the HTTP and embedded transports.
/// </summary>
public class CypherUpdateDeleteTests
{
    private static readonly Uri Endpoint = new("http://localhost:4747/");

    // ── SET property ─────────────────────────────────────────────────

    [Test]
    public async Task SetProperty_SendsCorrectQuery()
    {
        const string json = """{"results":[{"columns":[],"data":[]}]}""";
        var handler = RecordingHttpHandler.WithJson(json);
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync(
            "MATCH (n:User {id: $id}) SET n.active = $val",
            new Dictionary<string, object?> { ["id"] = "u1", ["val"] = true });

        await Assert.That(handler.LastRequestJson).IsNotNull();
        await Assert.That(handler.LastRequestJson!).Contains("SET n.active");
    }

    [Test]
    public async Task SetMultipleProperties_SendsCorrectQuery()
    {
        const string json = """{"results":[{"columns":[],"data":[]}]}""";
        var handler = RecordingHttpHandler.WithJson(json);
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync(
            "MATCH (n:Product {id: $id}) SET n.name = $name, n.price = $price RETURN n");

        await Assert.That(handler.LastRequestJson).IsNotNull();
        await Assert.That(handler.LastRequestJson!).Contains("SET n.name");
    }

    [Test]
    public async Task SetLabel_SendsCorrectQuery()
    {
        const string json = """{"results":[{"columns":["n"],"data":[]}]}""";
        var handler = RecordingHttpHandler.WithJson(json);
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync(
            "MATCH (n {id: $id}) SET n:Premium RETURN n");

        await Assert.That(handler.LastRequestJson!).Contains("SET n:Premium");
    }

    // ── REMOVE property / label ───────────────────────────────────────

    [Test]
    public async Task RemoveProperty_SendsCorrectQuery()
    {
        const string json = """{"results":[{"columns":[],"data":[]}]}""";
        var handler = RecordingHttpHandler.WithJson(json);
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync(
            "MATCH (n:User {id: $id}) REMOVE n.tempFlag");

        await Assert.That(handler.LastRequestJson!).Contains("REMOVE n.tempFlag");
    }

    [Test]
    public async Task RemoveLabel_SendsCorrectQuery()
    {
        const string json = """{"results":[{"columns":[],"data":[]}]}""";
        var handler = RecordingHttpHandler.WithJson(json);
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync(
            "MATCH (n:Premium {id: $id}) REMOVE n:Premium");

        await Assert.That(handler.LastRequestJson!).Contains("REMOVE n:Premium");
    }

    // ── DELETE ────────────────────────────────────────────────────────

    [Test]
    public async Task DeleteNode_SendsCorrectQuery()
    {
        const string json = """{"results":[{"columns":[],"data":[]}]}""";
        var handler = RecordingHttpHandler.WithJson(json);
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync(
            "MATCH (n:Temp) DELETE n");

        await Assert.That(handler.LastRequestJson!).Contains("DELETE n");
    }

    [Test]
    public async Task DeleteRelationship_SendsCorrectQuery()
    {
        const string json = """{"results":[{"columns":[],"data":[]}]}""";
        var handler = RecordingHttpHandler.WithJson(json);
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync(
            "MATCH (a)-[r:KNOWS]->(b) DELETE r");

        await Assert.That(handler.LastRequestJson!).Contains("DELETE r");
    }

    // ── DETACH DELETE ─────────────────────────────────────────────────

    [Test]
    public async Task DetachDelete_SendsCorrectQuery()
    {
        const string json = """{"results":[{"columns":[],"data":[]}]}""";
        var handler = RecordingHttpHandler.WithJson(json);
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync(
            "MATCH (n:User {id: $id}) DETACH DELETE n",
            new Dictionary<string, object?> { ["id"] = "u1" });

        await Assert.That(handler.LastRequestJson!).Contains("DETACH DELETE n");
    }

    [Test]
    public async Task DetachDeleteAll_SendsCorrectQuery()
    {
        const string json = """{"results":[{"columns":[],"data":[]}]}""";
        var handler = RecordingHttpHandler.WithJson(json);
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync("MATCH (n) DETACH DELETE n");

        await Assert.That(handler.LastRequestJson!).Contains("DETACH DELETE n");
    }

    // ── MERGE ─────────────────────────────────────────────────────────

    [Test]
    public async Task MergeNode_SendsCorrectQuery()
    {
        const string json = """{"results":[{"columns":["n"],"data":[]}]}""";
        var handler = RecordingHttpHandler.WithJson(json);
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync(
            "MERGE (n:User {email: $email}) RETURN n",
            new Dictionary<string, object?> { ["email"] = "test@example.com" });

        await Assert.That(handler.LastRequestJson!).Contains("MERGE");
        await Assert.That(handler.LastRequestJson!).Contains("email");
    }

    [Test]
    public async Task MergeWithOnCreate_SendsCorrectQuery()
    {
        const string json = """{"results":[{"columns":["n"],"data":[]}]}""";
        var handler = RecordingHttpHandler.WithJson(json);
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync(
            "MERGE (n:User {email: $email}) ON CREATE SET n.createdAt = $ts RETURN n");

        await Assert.That(handler.LastRequestJson!).Contains("ON CREATE SET");
    }

    [Test]
    public async Task MergeWithOnMatch_SendsCorrectQuery()
    {
        const string json = """{"results":[{"columns":["n"],"data":[]}]}""";
        var handler = RecordingHttpHandler.WithJson(json);
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync(
            "MERGE (n:User {email: $email}) ON MATCH SET n.updatedAt = $ts RETURN n");

        await Assert.That(handler.LastRequestJson!).Contains("ON MATCH SET");
    }

    [Test]
    public async Task MergeRelationship_SendsCorrectQuery()
    {
        const string json = """{"results":[{"columns":[],"data":[]}]}""";
        var handler = RecordingHttpHandler.WithJson(json);
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync(
            "MATCH (a:User {id: $aId}), (b:User {id: $bId}) MERGE (a)-[:KNOWS]->(b)");

        await Assert.That(handler.LastRequestJson!).Contains("MERGE (a)-[:KNOWS]->(b)");
    }

    // ── Embedded transport parity ─────────────────────────────────────

    [Test]
    public async Task Embedded_SetProperty_SendsCorrectJson()
    {
        const string json = """{"results":[{"columns":[],"data":[]}]}""";
        var bridge = new FakeNativeBridge(json);
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync(
            "MATCH (n:User {id: $id}) SET n.score = $score",
            new Dictionary<string, object?> { ["id"] = "u2", ["score"] = 99 });

        var req = JsonDocument.Parse(bridge.LastRequestJson!);
        await Assert.That(req.RootElement.GetProperty("query").GetString())
            .Contains("SET n.score");
    }

    [Test]
    public async Task Embedded_Merge_SendsCorrectJson()
    {
        const string json = """{"results":[{"columns":["n"],"data":[]}]}""";
        var bridge = new FakeNativeBridge(json);
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync(
            "MERGE (n:Tag {name: $name}) RETURN n",
            new Dictionary<string, object?> { ["name"] = "dotnet" });

        var req = JsonDocument.Parse(bridge.LastRequestJson!);
        await Assert.That(req.RootElement.GetProperty("query").GetString()).Contains("MERGE");
    }
}
