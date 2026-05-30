using System.Text.Json;
using System.Text.Json.Serialization;
using LoraDb.Client.Tests.Helpers;
using TUnit.Assertions.Extensions;

namespace LoraDb.Client.Tests;

/// <summary>
/// Unit tests for <see cref="LoraDbClientCrudExtensions"/>.
/// All tests exercise Cypher generation and result deserialization using the fake
/// embedded transport — no real database is required.
/// </summary>
public class CrudExtensionsTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static FakeNativeBridge NodeBridge(int id, string label, string propertiesJson)
        => new($$$"""{"rows":[{"n":{"id":{{{id}}},"labels":["{{{label}}}"],"properties":{{{propertiesJson}}}}}]}""");

    private static FakeNativeBridge EmptyBridge()
        => new("""{"rows":[]}""");

    private static async Task<JsonDocument> ParseLastRequestAsync(FakeNativeBridge bridge)
        => JsonDocument.Parse(bridge.LastRequestJson!);

    // ── CreateNodeAsync (with properties) ─────────────────────────────────────

    [Test]
    public async Task CreateNode_WithProperties_GeneratesCorrectCypher()
    {
        var bridge = NodeBridge(1, "Person", """{"name":"Alice","age":30}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        _ = await client.CreateNodeAsync<NodeRow>("Person",
            new Dictionary<string, object?> { ["name"] = "Alice", ["age"] = 30 });

        using var doc = await ParseLastRequestAsync(bridge);
        var query = doc.RootElement.GetProperty("query").GetString()!;
        await Assert.That(query).StartsWith("CREATE (n:Person {");
        await Assert.That(query).Contains("name: $create_name");
        await Assert.That(query).Contains("age: $create_age");
        await Assert.That(query).EndsWith("RETURN n");
    }

    [Test]
    public async Task CreateNode_WithProperties_PassesParametersCorrectly()
    {
        var bridge = NodeBridge(1, "Person", """{"name":"Alice","age":30}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        _ = await client.CreateNodeAsync<NodeRow>("Person",
            new Dictionary<string, object?> { ["name"] = "Alice", ["age"] = 30 });

        using var doc = await ParseLastRequestAsync(bridge);
        var prms = doc.RootElement.GetProperty("params");
        await Assert.That(prms.GetProperty("create_name").GetString()).IsEqualTo("Alice");
        await Assert.That(prms.GetProperty("create_age").GetInt32()).IsEqualTo(30);
    }

    [Test]
    public async Task CreateNode_WithProperties_ReturnsDeserializedRow()
    {
        var bridge = NodeBridge(42, "Person", """{"name":"Alice","age":30}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var row = await client.CreateNodeAsync<NodeRow>("Person",
            new Dictionary<string, object?> { ["name"] = "Alice", ["age"] = 30 });

        await Assert.That(row.N.Id).IsEqualTo(42);
        await Assert.That(row.N.Labels[0]).IsEqualTo("Person");
    }

    // ── CreateNodeAsync (no properties) ───────────────────────────────────────

    [Test]
    public async Task CreateNode_WithoutProperties_GeneratesCorrectCypher()
    {
        var bridge = NodeBridge(1, "Empty", "{}");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        _ = await client.CreateNodeAsync<NodeRow>("Empty");

        using var doc = await ParseLastRequestAsync(bridge);
        var query = doc.RootElement.GetProperty("query").GetString()!;
        await Assert.That(query).IsEqualTo("CREATE (n:Empty) RETURN n");
    }

    [Test]
    public async Task CreateNode_EmptyLabel_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () => await client.CreateNodeAsync<NodeRow>("  ", new Dictionary<string, object?> { ["k"] = "v" }))
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task CreateNode_EmptyProperties_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () => await client.CreateNodeAsync<NodeRow>("Label", new Dictionary<string, object?>()))
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task CreateNode_NullClient_ThrowsArgumentNullException()
    {
        await Assert.That(async () => await ((ILoraDbClient)null!).CreateNodeAsync<NodeRow>("Label", new Dictionary<string, object?> { ["k"] = "v" }))
            .ThrowsException()
            .And.IsTypeOf<ArgumentNullException>();
    }

    // ── FindNodesAsync ─────────────────────────────────────────────────────────

    [Test]
    public async Task FindNodes_WithFilters_GeneratesCorrectCypher()
    {
        var bridge = new FakeNativeBridge("""{"rows":[{"n":{"id":1,"labels":["User"],"properties":{"name":"Alice"}}}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        _ = await client.FindNodesAsync<NodeRow>("User",
            new Dictionary<string, object?> { ["name"] = "Alice" });

        using var doc = await ParseLastRequestAsync(bridge);
        var query = doc.RootElement.GetProperty("query").GetString()!;
        await Assert.That(query).IsEqualTo("MATCH (n:User {name: $filter_name}) RETURN n");
    }

    [Test]
    public async Task FindNodes_WithFilters_PassesParameters()
    {
        var bridge = new FakeNativeBridge("""{"rows":[]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        _ = await client.FindNodesAsync<NodeRow>("User",
            new Dictionary<string, object?> { ["name"] = "Alice", ["age"] = 30 });

        using var doc = await ParseLastRequestAsync(bridge);
        var prms = doc.RootElement.GetProperty("params");
        await Assert.That(prms.GetProperty("filter_name").GetString()).IsEqualTo("Alice");
        await Assert.That(prms.GetProperty("filter_age").GetInt32()).IsEqualTo(30);
    }

    [Test]
    public async Task FindNodes_WithoutFilters_GeneratesCypherWithoutPropertyMap()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        _ = await client.FindNodesAsync<NodeRow>("Tag");

        using var doc = await ParseLastRequestAsync(bridge);
        var query = doc.RootElement.GetProperty("query").GetString()!;
        await Assert.That(query).IsEqualTo("MATCH (n:Tag) RETURN n");
    }

    [Test]
    public async Task FindNodes_EmptyFilterDict_TreatedAsNoFilter()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        _ = await client.FindNodesAsync<NodeRow>("Tag", new Dictionary<string, object?>());

        using var doc = await ParseLastRequestAsync(bridge);
        var query = doc.RootElement.GetProperty("query").GetString()!;
        await Assert.That(query).IsEqualTo("MATCH (n:Tag) RETURN n");
    }

    [Test]
    public async Task FindNodes_ReturnsAllDeserializedRows()
    {
        var bridge = new FakeNativeBridge(
            """{"rows":[{"n":{"id":1,"labels":["P"],"properties":{"name":"A"}}},{"n":{"id":2,"labels":["P"],"properties":{"name":"B"}}}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var rows = await client.FindNodesAsync<NodeRow>("P");

        await Assert.That(rows.Count).IsEqualTo(2);
        await Assert.That(rows[0].N.Id).IsEqualTo(1);
        await Assert.That(rows[1].N.Id).IsEqualTo(2);
    }

    // ── FindNodeAsync ──────────────────────────────────────────────────────────

    [Test]
    public async Task FindNode_WithFilters_AddsLimitOne()
    {
        var bridge = NodeBridge(1, "P", "{}");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        _ = await client.FindNodeAsync<NodeRow>("P", new Dictionary<string, object?> { ["id"] = 1 });

        using var doc = await ParseLastRequestAsync(bridge);
        var query = doc.RootElement.GetProperty("query").GetString()!;
        await Assert.That(query).EndsWith("LIMIT 1");
    }

    [Test]
    public async Task FindNode_NoMatch_ReturnsNull()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var row = await client.FindNodeAsync<NodeRow>("Ghost");

        await Assert.That(row).IsNull();
    }

    [Test]
    public async Task FindNode_WhenMatched_ReturnsFirstRow()
    {
        var bridge = NodeBridge(7, "P", """{"x":1}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var row = await client.FindNodeAsync<NodeRow>("P");

        await Assert.That(row).IsNotNull();
        await Assert.That(row!.N.Id).IsEqualTo(7);
    }

    // ── UpdateNodesAsync ───────────────────────────────────────────────────────

    [Test]
    public async Task UpdateNodes_GeneratesMatchSetReturnN()
    {
        var bridge = NodeBridge(1, "User", """{"active":false}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        _ = await client.UpdateNodesAsync<NodeRow>("User",
            match: new Dictionary<string, object?> { ["id"] = "u1" },
            properties: new Dictionary<string, object?> { ["active"] = false });

        using var doc = await ParseLastRequestAsync(bridge);
        var query = doc.RootElement.GetProperty("query").GetString()!;
        await Assert.That(query).StartsWith("MATCH (n:User {");
        await Assert.That(query).Contains("id: $match_id");
        await Assert.That(query).Contains("SET n.active = $set_active");
        await Assert.That(query).EndsWith("RETURN n");
    }

    [Test]
    public async Task UpdateNodes_PassesMatchAndSetParameters()
    {
        var bridge = NodeBridge(1, "U", "{}");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        _ = await client.UpdateNodesAsync<NodeRow>("U",
            match: new Dictionary<string, object?> { ["id"] = "u1" },
            properties: new Dictionary<string, object?> { ["score"] = 99 });

        using var doc = await ParseLastRequestAsync(bridge);
        var prms = doc.RootElement.GetProperty("params");
        await Assert.That(prms.GetProperty("match_id").GetString()).IsEqualTo("u1");
        await Assert.That(prms.GetProperty("set_score").GetInt32()).IsEqualTo(99);
    }

    [Test]
    public async Task UpdateNodes_EmptyMatch_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () => await client.UpdateNodesAsync<NodeRow>("U",
                match: new Dictionary<string, object?>(),
                properties: new Dictionary<string, object?> { ["x"] = 1 }))
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task UpdateNodes_EmptyProperties_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () => await client.UpdateNodesAsync<NodeRow>("U",
                match: new Dictionary<string, object?> { ["id"] = 1 },
                properties: new Dictionary<string, object?>()))
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task UpdateNodes_MatchAndSetKeysDoNotCollide()
    {
        var bridge = NodeBridge(1, "U", """{"name":"Bob"}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        // Both match and set have a "name" key — the prefixes keep them separate
        _ = await client.UpdateNodesAsync<NodeRow>("U",
            match: new Dictionary<string, object?> { ["name"] = "Alice" },
            properties: new Dictionary<string, object?> { ["name"] = "Bob" });

        using var doc = await ParseLastRequestAsync(bridge);
        var prms = doc.RootElement.GetProperty("params");
        await Assert.That(prms.GetProperty("match_name").GetString()).IsEqualTo("Alice");
        await Assert.That(prms.GetProperty("set_name").GetString()).IsEqualTo("Bob");
    }

    // ── DeleteNodesAsync ───────────────────────────────────────────────────────

    [Test]
    public async Task DeleteNodes_WithMatchAndDetach_GeneratesDetachDelete()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await client.DeleteNodesAsync("Person",
            match: new Dictionary<string, object?> { ["id"] = "p1" },
            detach: true);

        using var doc = await ParseLastRequestAsync(bridge);
        var query = doc.RootElement.GetProperty("query").GetString()!;
        await Assert.That(query).Contains("DETACH DELETE n");
        await Assert.That(query).Contains("id: $match_id");
    }

    [Test]
    public async Task DeleteNodes_WithoutDetach_GeneratesPlainDelete()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await client.DeleteNodesAsync("Person",
            match: new Dictionary<string, object?> { ["id"] = "p1" },
            detach: false);

        using var doc = await ParseLastRequestAsync(bridge);
        var query = doc.RootElement.GetProperty("query").GetString()!;
        await Assert.That(query).Contains("DELETE n");
        await Assert.That(query).DoesNotContain("DETACH");
    }

    [Test]
    public async Task DeleteNodes_WithoutMatch_DeletesAllNodesWithLabel()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await client.DeleteNodesAsync("TempNode");

        using var doc = await ParseLastRequestAsync(bridge);
        var query = doc.RootElement.GetProperty("query").GetString()!;
        await Assert.That(query).IsEqualTo("MATCH (n:TempNode) DETACH DELETE n");
    }

    [Test]
    public async Task DeleteNodes_PassesMatchParameters()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await client.DeleteNodesAsync("P", new Dictionary<string, object?> { ["id"] = 42 });

        using var doc = await ParseLastRequestAsync(bridge);
        var prms = doc.RootElement.GetProperty("params");
        await Assert.That(prms.GetProperty("match_id").GetInt32()).IsEqualTo(42);
    }

    // ── MergeNodeAsync ─────────────────────────────────────────────────────────

    [Test]
    public async Task MergeNode_GeneratesMergeCypher()
    {
        var bridge = NodeBridge(1, "User", """{"email":"alice@example.com"}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        _ = await client.MergeNodeAsync<NodeRow>("User",
            new Dictionary<string, object?> { ["email"] = "alice@example.com" });

        using var doc = await ParseLastRequestAsync(bridge);
        var query = doc.RootElement.GetProperty("query").GetString()!;
        await Assert.That(query).StartsWith("MERGE (n:User {");
        await Assert.That(query).Contains("email: $merge_email");
        await Assert.That(query).EndsWith("RETURN n");
    }

    [Test]
    public async Task MergeNode_PassesParameters()
    {
        var bridge = NodeBridge(1, "Tag", """{"name":"dotnet"}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        _ = await client.MergeNodeAsync<NodeRow>("Tag",
            new Dictionary<string, object?> { ["name"] = "dotnet" });

        using var doc = await ParseLastRequestAsync(bridge);
        await Assert.That(doc.RootElement.GetProperty("params").GetProperty("merge_name").GetString())
            .IsEqualTo("dotnet");
    }

    [Test]
    public async Task MergeNode_EmptyMergeProperties_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () => await client.MergeNodeAsync<NodeRow>("Tag", new Dictionary<string, object?>()))
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task MergeNode_ReturnsDeserializedRows()
    {
        var bridge = NodeBridge(5, "User", """{"email":"x@x.com"}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var row = await client.MergeNodeAsync<NodeRow>("User",
            new Dictionary<string, object?> { ["email"] = "x@x.com" });

        await Assert.That(row.N.Id).IsEqualTo(5);
    }

    // ── CreateBatch ────────────────────────────────────────────────────────────

    [Test]
    public async Task CreateBatch_ReturnsNewBatch()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var batch = client.CreateBatch();

        await Assert.That(batch).IsNotNull();
        await Assert.That(batch.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CreateBatch_NullClient_ThrowsArgumentNullException()
    {
        await Assert.That(() => ((ILoraDbClient)null!).CreateBatch())
            .ThrowsException()
            .And.IsTypeOf<ArgumentNullException>();
    }

    // ── DTOs ───────────────────────────────────────────────────────────────────

    public sealed class NodeRow
    {
        [JsonPropertyName("n")]
        public NodeData N { get; init; } = null!;
    }

    public sealed class NodeData
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("labels")]
        public List<string> Labels { get; init; } = new();

        [JsonPropertyName("properties")]
        public JsonElement Properties { get; init; }
    }
}
