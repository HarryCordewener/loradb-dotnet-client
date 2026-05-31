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

    [Test]
    public async Task CreateNode_InvalidLabel_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () => await client.CreateNodeAsync<NodeRow>(
                "Person) RETURN n //", new Dictionary<string, object?> { ["k"] = "v" }))
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task CreateNode_InvalidPropertyKey_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () => await client.CreateNodeAsync<NodeRow>(
                "Person", new Dictionary<string, object?> { ["k}) RETURN 1 //"] = "v" }))
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
    }

    // ── CreateNodeAsync (no properties) — additional coverage ─────────────────

    [Test]
    public async Task CreateNode_WithoutProperties_NullClient_ThrowsArgumentNullException()
    {
        await Assert.That(async () => await ((ILoraDbClient)null!).CreateNodeAsync<NodeRow>("Label"))
            .ThrowsException()
            .And.IsTypeOf<ArgumentNullException>();
    }

    [Test]
    public async Task CreateNode_WithoutProperties_EmptyLabel_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () => await client.CreateNodeAsync<NodeRow>("  "))
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task CreateNode_WithoutProperties_ReturnsDeserializedRow()
    {
        var bridge = NodeBridge(10, "Empty", "{}");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var row = await client.CreateNodeAsync<NodeRow>("Empty");

        await Assert.That(row.N.Id).IsEqualTo(10);
        await Assert.That(row.N.Labels[0]).IsEqualTo("Empty");
    }

    [Test]
    public async Task CreateNode_WithoutProperties_InvalidLabel_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () => await client.CreateNodeAsync<NodeRow>("Bad Label!"))
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task CreateNode_NoRowsReturned_ThrowsInvalidOperationException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () => await client.CreateNodeAsync<NodeRow>("Ghost",
                new Dictionary<string, object?> { ["k"] = "v" }))
            .ThrowsException()
            .And.IsTypeOf<InvalidOperationException>();
    }

    // ── FindNodesAsync ─────────────────────────────────────────────────────────

    [Test]
    public async Task FindNodes_WithFilters_GeneratesCorrectCypher()
    {
        var bridge = new FakeNativeBridge("""{"rows":[{"n":{"id":1,"labels":["User"],"properties":{"name":"Alice"}}}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await foreach (var _ in client.FindNodesAsync<NodeRow>("User",
            new Dictionary<string, object?> { ["name"] = "Alice" }))
        { }

        using var doc = await ParseLastRequestAsync(bridge);
        var query = doc.RootElement.GetProperty("query").GetString()!;
        await Assert.That(query).IsEqualTo("MATCH (n:User {name: $filter_name}) RETURN n");
    }

    [Test]
    public async Task FindNodes_WithFilters_PassesParameters()
    {
        var bridge = new FakeNativeBridge("""{"rows":[]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await foreach (var _ in client.FindNodesAsync<NodeRow>("User",
            new Dictionary<string, object?> { ["name"] = "Alice", ["age"] = 30 }))
        { }

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

        await foreach (var _ in client.FindNodesAsync<NodeRow>("Tag"))
        { }

        using var doc = await ParseLastRequestAsync(bridge);
        var query = doc.RootElement.GetProperty("query").GetString()!;
        await Assert.That(query).IsEqualTo("MATCH (n:Tag) RETURN n");
    }

    [Test]
    public async Task FindNodes_EmptyFilterDict_TreatedAsNoFilter()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await foreach (var _ in client.FindNodesAsync<NodeRow>("Tag", new Dictionary<string, object?>()))
        { }

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

        var rows = new List<NodeRow>();
        await foreach (var row in client.FindNodesAsync<NodeRow>("P"))
            rows.Add(row);

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

        await foreach (var _ in client.UpdateNodesAsync<NodeRow>("User",
            match: new Dictionary<string, object?> { ["id"] = "u1" },
            properties: new Dictionary<string, object?> { ["active"] = false }))
        { }

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

        await foreach (var _ in client.UpdateNodesAsync<NodeRow>("U",
            match: new Dictionary<string, object?> { ["id"] = "u1" },
            properties: new Dictionary<string, object?> { ["score"] = 99 }))
        { }

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

        await Assert.That(async () =>
            {
                await foreach (var _ in client.UpdateNodesAsync<NodeRow>("U",
                                   match: new Dictionary<string, object?>(),
                                   properties: new Dictionary<string, object?> { ["x"] = 1 }))
                { }
            })
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task UpdateNodes_EmptyProperties_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () =>
            {
                await foreach (var _ in client.UpdateNodesAsync<NodeRow>("U",
                                   match: new Dictionary<string, object?> { ["id"] = 1 },
                                   properties: new Dictionary<string, object?>()))
                { }
            })
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task UpdateNodes_MatchAndSetKeysDoNotCollide()
    {
        var bridge = NodeBridge(1, "U", """{"name":"Bob"}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        // Both match and set have a "name" key — the prefixes keep them separate
        await foreach (var _ in client.UpdateNodesAsync<NodeRow>("U",
            match: new Dictionary<string, object?> { ["name"] = "Alice" },
            properties: new Dictionary<string, object?> { ["name"] = "Bob" }))
        { }

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

    [Test]
    public async Task DeleteNodes_NullClient_ThrowsArgumentNullException()
    {
        await Assert.That(async () => await ((ILoraDbClient)null!).DeleteNodesAsync("P"))
            .ThrowsException()
            .And.IsTypeOf<ArgumentNullException>();
    }

    [Test]
    public async Task DeleteNodes_InvalidLabel_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () => await client.DeleteNodesAsync("123BadLabel"))
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task DeleteNodes_InvalidMatchKey_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () => await client.DeleteNodesAsync("P",
                new Dictionary<string, object?> { ["id) DELETE ALL //"] = 1 }))
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
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

    [Test]
    public async Task MergeNode_NullClient_ThrowsArgumentNullException()
    {
        await Assert.That(async () => await ((ILoraDbClient)null!).MergeNodeAsync<NodeRow>(
                "Tag", new Dictionary<string, object?> { ["k"] = "v" }))
            .ThrowsException()
            .And.IsTypeOf<ArgumentNullException>();
    }

    [Test]
    public async Task MergeNode_InvalidLabel_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () => await client.MergeNodeAsync<NodeRow>(
                "User) RETURN 1 //", new Dictionary<string, object?> { ["k"] = "v" }))
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task MergeNode_InvalidPropertyKey_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () => await client.MergeNodeAsync<NodeRow>(
                "Tag", new Dictionary<string, object?> { ["k} RETURN 1 //"] = "v" }))
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task MergeNode_NoRowsReturned_ThrowsInvalidOperationException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () => await client.MergeNodeAsync<NodeRow>(
                "Tag", new Dictionary<string, object?> { ["k"] = "v" }))
            .ThrowsException()
            .And.IsTypeOf<InvalidOperationException>();
    }

    // ── FindNodesAsync — additional injection/null coverage ────────────────────

    [Test]
    public async Task FindNodes_NullClient_ThrowsArgumentNullException()
    {
        await Assert.That(async () =>
            {
                await foreach (var _ in ((ILoraDbClient)null!).FindNodesAsync<NodeRow>("Tag"))
                { }
            })
            .ThrowsException()
            .And.IsTypeOf<ArgumentNullException>();
    }

    [Test]
    public async Task FindNodes_InvalidLabel_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () =>
            {
                await foreach (var _ in client.FindNodesAsync<NodeRow>("Tag) RETURN 1 //"))
                { }
            })
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task FindNodes_InvalidFilterKey_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () =>
            {
                await foreach (var _ in client.FindNodesAsync<NodeRow>("Tag",
                    new Dictionary<string, object?> { ["k}) RETURN 1 //"] = "v" }))
                { }
            })
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
    }

    // ── FindNodeAsync — additional injection/null coverage ─────────────────────

    [Test]
    public async Task FindNode_NullClient_ThrowsArgumentNullException()
    {
        await Assert.That(async () => await ((ILoraDbClient)null!).FindNodeAsync<NodeRow>("Tag"))
            .ThrowsException()
            .And.IsTypeOf<ArgumentNullException>();
    }

    [Test]
    public async Task FindNode_InvalidLabel_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () => await client.FindNodeAsync<NodeRow>("Tag) RETURN 1 //"))
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task FindNode_InvalidFilterKey_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () => await client.FindNodeAsync<NodeRow>("Tag",
                new Dictionary<string, object?> { ["k}) RETURN 1 //"] = "v" }))
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
    }

    // ── UpdateNodesAsync — additional injection/null coverage ──────────────────

    [Test]
    public async Task UpdateNodes_NullClient_ThrowsArgumentNullException()
    {
        await Assert.That(async () =>
            {
                await foreach (var _ in ((ILoraDbClient)null!).UpdateNodesAsync<NodeRow>(
                    "U",
                    new Dictionary<string, object?> { ["id"] = 1 },
                    new Dictionary<string, object?> { ["x"] = 2 }))
                { }
            })
            .ThrowsException()
            .And.IsTypeOf<ArgumentNullException>();
    }

    [Test]
    public async Task UpdateNodes_InvalidLabel_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () =>
            {
                await foreach (var _ in client.UpdateNodesAsync<NodeRow>(
                    "U) RETURN 1 //",
                    new Dictionary<string, object?> { ["id"] = 1 },
                    new Dictionary<string, object?> { ["x"] = 2 }))
                { }
            })
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task UpdateNodes_InvalidMatchKey_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () =>
            {
                await foreach (var _ in client.UpdateNodesAsync<NodeRow>(
                    "U",
                    new Dictionary<string, object?> { ["id}) RETURN 1 //"] = 1 },
                    new Dictionary<string, object?> { ["x"] = 2 }))
                { }
            })
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task UpdateNodes_InvalidSetKey_ThrowsArgumentException()
    {
        var bridge = EmptyBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        await Assert.That(async () =>
            {
                await foreach (var _ in client.UpdateNodesAsync<NodeRow>(
                    "U",
                    new Dictionary<string, object?> { ["id"] = 1 },
                    new Dictionary<string, object?> { ["x}) SET n.y = 1 //"] = 2 }))
                { }
            })
            .ThrowsException()
            .And.IsTypeOf<ArgumentException>();
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
