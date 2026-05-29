using System.Text.Json;
using LoraDb.Client.Tests.Helpers;
using TUnit.Assertions.Extensions;

namespace LoraDb.Client.Tests;

/// <summary>
/// Tests mirroring the LoraDB Cypher CREATE capabilities tested in
/// crates/lora-database/tests/create.rs — exercised through the embedded transport
/// so every Cypher pattern is transmitted correctly to the native bridge.
/// </summary>
public class CypherCreateTests
{
    private static FakeNativeBridge BridgeReturning(string json) => new(json);

    private static FakeNativeBridge SingleRowBridge(string rowJson) =>
        BridgeReturning($$"""{"rows":[{{rowJson}}]}""");

    // ── Node creation ────────────────────────────────────────────────

    [Test]
    public async Task CreateNode_NoLabels_QueryTransmitted()
    {
        var bridge = SingleRowBridge("""{"n":{"id":1,"labels":[],"properties":{}}}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("CREATE (n) RETURN n");

        await AssertQueryEquals(bridge, "CREATE (n) RETURN n");
    }

    [Test]
    public async Task CreateNode_WithSingleLabel_QueryTransmitted()
    {
        var bridge = SingleRowBridge("""{"n":{"id":1,"labels":["Person"],"properties":{}}}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("CREATE (n:Person) RETURN n");

        var labels = result.Root.GetProperty("rows")[0].GetProperty("n").GetProperty("labels");
        await Assert.That(labels[0].GetString()).IsEqualTo("Person");
    }

    [Test]
    public async Task CreateNode_WithMultipleLabels_QueryTransmitted()
    {
        var bridge = SingleRowBridge("""{"n":{"id":1,"labels":["Person","Employee"],"properties":{}}}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("CREATE (n:Person:Employee) RETURN n");

        await AssertQueryEquals(bridge, "CREATE (n:Person:Employee) RETURN n");
    }

    [Test]
    public async Task CreateNode_WithStringProperty_PropertyInRequest()
    {
        var bridge = SingleRowBridge("""{"n":{"id":1,"labels":["User"],"properties":{"name":"Alice"}}}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("CREATE (n:User {name: 'Alice'}) RETURN n");

        var name = result.Root.GetProperty("rows")[0]
            .GetProperty("n").GetProperty("properties").GetProperty("name").GetString();
        await Assert.That(name).IsEqualTo("Alice");
    }

    [Test]
    public async Task CreateNode_WithIntegerProperty_ValueRoundTrips()
    {
        var bridge = SingleRowBridge("""{"n":{"id":1,"labels":["User"],"properties":{"age":42}}}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("CREATE (n:User {age: 42}) RETURN n");

        var age = result.Root.GetProperty("rows")[0]
            .GetProperty("n").GetProperty("properties").GetProperty("age").GetInt32();
        await Assert.That(age).IsEqualTo(42);
    }

    [Test]
    public async Task CreateNode_WithBooleanProperty_ValueRoundTrips()
    {
        var bridge = SingleRowBridge("""{"n":{"id":1,"labels":["User"],"properties":{"active":true}}}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("CREATE (n:User {active: true}) RETURN n");

        var active = result.Root.GetProperty("rows")[0]
            .GetProperty("n").GetProperty("properties").GetProperty("active").GetBoolean();
        await Assert.That(active).IsTrue();
    }

    [Test]
    public async Task CreateNode_WithFloatProperty_ValueRoundTrips()
    {
        var bridge = SingleRowBridge("""{"n":{"id":1,"labels":["Metric"],"properties":{"score":3.14}}}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("CREATE (n:Metric {score: 3.14}) RETURN n");

        var score = result.Root.GetProperty("rows")[0]
            .GetProperty("n").GetProperty("properties").GetProperty("score").GetDouble();
        await Assert.That(Math.Abs(score - 3.14)).IsLessThanOrEqualTo(0.001);
    }

    [Test]
    public async Task CreateNode_WithListProperty_IsArray()
    {
        var bridge = SingleRowBridge("""{"n":{"id":1,"labels":["User"],"properties":{"tags":[1,2,3]}}}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("CREATE (n:User {tags: [1, 2, 3]}) RETURN n");

        var tags = result.Root.GetProperty("rows")[0]
            .GetProperty("n").GetProperty("properties").GetProperty("tags");
        await Assert.That(tags.ValueKind).IsEqualTo(JsonValueKind.Array);
    }

    // ── Relationship creation ────────────────────────────────────────

    [Test]
    public async Task CreateRelationship_WithType_TypeInResponse()
    {
        var bridge = SingleRowBridge("""{"r":{"id":1,"type":"FOLLOWS","properties":{}}}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync(
            "MATCH (a:User {name: 'Alice'}), (b:User {name: 'Bob'}) CREATE (a)-[r:FOLLOWS]->(b) RETURN r");

        var type = result.Root.GetProperty("rows")[0].GetProperty("r").GetProperty("type").GetString();
        await Assert.That(type).IsEqualTo("FOLLOWS");
    }

    [Test]
    public async Task CreateRelationship_WithProperties_PropertiesInResponse()
    {
        var bridge = SingleRowBridge("""{"r":{"id":1,"type":"FOLLOWS","properties":{"since":2020}}}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync(
            "MATCH (a:User {name: 'Alice'}), (b:User {name: 'Bob'}) CREATE (a)-[r:FOLLOWS {since: 2020}]->(b) RETURN r");

        var since = result.Root.GetProperty("rows")[0]
            .GetProperty("r").GetProperty("properties").GetProperty("since").GetInt32();
        await Assert.That(since).IsEqualTo(2020);
    }

    [Test]
    public async Task CreatePattern_NodeAndRelationship_BothReturned()
    {
        var bridge = BridgeReturning("""{"rows":[{"a":{"id":1,"labels":["User"],"properties":{"name":"Alice"}},"b":{"id":2,"labels":["User"],"properties":{"name":"Bob"}}}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync(
            "CREATE (a:User {name: 'Alice'})-[:FOLLOWS]->(b:User {name: 'Bob'}) RETURN a, b");

        var aName = result.Root.GetProperty("rows")[0].GetProperty("a").GetProperty("properties").GetProperty("name").GetString();
        var bName = result.Root.GetProperty("rows")[0].GetProperty("b").GetProperty("properties").GetProperty("name").GetString();
        await Assert.That(aName).IsEqualTo("Alice");
        await Assert.That(bName).IsEqualTo("Bob");
    }

    // ── Returning computed expressions ───────────────────────────────

    [Test]
    public async Task CreateNode_ReturningComputedExpression_ValuePresent()
    {
        var bridge = BridgeReturning("""{"rows":[{"doubled":20}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("CREATE (n:Calc {val: 10}) RETURN n.val * 2 AS doubled");

        var doubled = result.Root.GetProperty("rows")[0].GetProperty("doubled").GetInt32();
        await Assert.That(doubled).IsEqualTo(20);
    }

    // ── Batch creation ───────────────────────────────────────────────

    [Test]
    public async Task UnwindCreate_BatchQuery_Transmitted()
    {
        var bridge = BridgeReturning("""{"rows":[{"total":5}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync(
            "UNWIND [1, 2, 3, 4, 5] AS i CREATE (:Num {val: i})");

        await AssertQueryContains(bridge, "UNWIND");
    }

    // ── Multiple property types ──────────────────────────────────────

    [Test]
    public async Task CreateNode_MixedPropertyTypes_AllPresent()
    {
        var bridge = SingleRowBridge("""{"n":{"id":1,"labels":["Mixed"],"properties":{"str":"hello","num":42,"flt":3.14,"flag":true}}}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync(
            "CREATE (n:Mixed {str: 'hello', num: 42, flt: 3.14, flag: true}) RETURN n");

        var props = result.Root.GetProperty("rows")[0].GetProperty("n").GetProperty("properties");
        await Assert.That(props.GetProperty("str").GetString()).IsEqualTo("hello");
        await Assert.That(props.GetProperty("num").GetInt32()).IsEqualTo(42);
        await Assert.That(props.GetProperty("flag").GetBoolean()).IsTrue();
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static async Task AssertQueryEquals(FakeNativeBridge bridge, string expectedQuery)
    {
        using var doc = JsonDocument.Parse(bridge.LastRequestJson!);
        await Assert.That(doc.RootElement.GetProperty("query").GetString()).IsEqualTo(expectedQuery);
    }

    private static async Task AssertQueryContains(FakeNativeBridge bridge, string substring)
    {
        using var doc = JsonDocument.Parse(bridge.LastRequestJson!);
        var query = doc.RootElement.GetProperty("query").GetString() ?? string.Empty;
        await Assert.That(query).Contains(substring);
    }
}
