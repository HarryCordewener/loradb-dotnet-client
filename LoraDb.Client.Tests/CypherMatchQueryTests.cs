using System.Text.Json;
using LoraDb.Client.Tests.Helpers;
using TUnit.Assertions.Extensions;

namespace LoraDb.Client.Tests;

/// <summary>
/// Tests mirroring crates/lora-database/tests/match.rs — MATCH, WHERE, ORDER BY,
/// SKIP/LIMIT, relationship traversal, path patterns, aggregation.
/// </summary>
public class CypherMatchQueryTests
{
    private static FakeNativeBridge BridgeReturning(string json) => new(json);

    // ── Basic MATCH ──────────────────────────────────────────────────

    [Test]
    public async Task Match_AllNodes_QueryTransmitted()
    {
        var bridge = BridgeReturning("""{"rows":[{"n":{"id":1}},{"n":{"id":2}}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("MATCH (n) RETURN n");

        await AssertQueryEquals(bridge, "MATCH (n) RETURN n");
    }

    [Test]
    public async Task Match_ByLabel_ReturnsFilteredRows()
    {
        var bridge = BridgeReturning("""{"rows":[{"name":"Alice"},{"name":"Bob"}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("MATCH (u:User) RETURN u.name AS name");

        await Assert.That(result.Root.GetProperty("rows").GetArrayLength()).IsEqualTo(2);
    }

    [Test]
    public async Task Match_WithWhereClause_QueryTransmitted()
    {
        var bridge = BridgeReturning("""{"rows":[{"name":"Alice"}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync(
            "MATCH (u:User) WHERE u.name = 'Alice' RETURN u.name AS name");

        await AssertQueryContains(bridge, "WHERE");
    }

    [Test]
    public async Task Match_ByRelationshipType_QueryTransmitted()
    {
        var bridge = BridgeReturning("""{"rows":[{"follower":"Alice","followee":"Bob"}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync(
            "MATCH (a:User)-[:FOLLOWS]->(b:User) RETURN a.name AS follower, b.name AS followee");

        await AssertQueryContains(bridge, "FOLLOWS");
    }

    // ── Aggregation ──────────────────────────────────────────────────

    [Test]
    public async Task Match_CountAggregation_ReturnsCount()
    {
        var bridge = BridgeReturning("""{"rows":[{"total":5}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("MATCH (n:User) RETURN count(n) AS total");

        await Assert.That(result.Root.GetProperty("rows")[0].GetProperty("total").GetInt32())
            .IsEqualTo(5);
    }

    [Test]
    public async Task Match_SumAggregation_ReturnsSumValue()
    {
        var bridge = BridgeReturning("""{"rows":[{"total":60}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("MATCH (s:Score) RETURN sum(s.val) AS total");

        await Assert.That(result.Root.GetProperty("rows")[0].GetProperty("total").GetInt32())
            .IsEqualTo(60);
    }

    // ── ORDER BY / SKIP / LIMIT ──────────────────────────────────────

    [Test]
    public async Task Match_WithOrderBy_QueryTransmitted()
    {
        var bridge = BridgeReturning("""{"rows":[{"name":"Alice"},{"name":"Bob"}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync(
            "MATCH (u:User) RETURN u.name AS name ORDER BY u.name ASC");

        await AssertQueryContains(bridge, "ORDER BY");
    }

    [Test]
    public async Task Match_WithLimit_QueryTransmitted()
    {
        var bridge = BridgeReturning("""{"rows":[{"name":"Alice"}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("MATCH (u:User) RETURN u.name AS name LIMIT 1");

        await AssertQueryContains(bridge, "LIMIT");
    }

    [Test]
    public async Task Match_WithSkipAndLimit_BothInQuery()
    {
        var bridge = BridgeReturning("""{"rows":[{"name":"Bob"}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync(
            "MATCH (u:User) RETURN u.name AS name SKIP 1 LIMIT 1");

        await AssertQueryContains(bridge, "SKIP");
        await AssertQueryContains(bridge, "LIMIT");
    }

    // ── WITH clause ──────────────────────────────────────────────────

    [Test]
    public async Task Match_WithWithClause_QueryTransmitted()
    {
        var bridge = BridgeReturning("""{"rows":[{"total":60}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync(
            "MATCH (s:Score) WITH sum(s.val) AS total RETURN total");

        await AssertQueryContains(bridge, "WITH");
    }

    // ── Result row structure ─────────────────────────────────────────

    [Test]
    public async Task Match_EmptyGraph_ReturnsEmptyRows()
    {
        var bridge = BridgeReturning("""{"rows":[]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("MATCH (n:NonExistent) RETURN n");

        await Assert.That(result.Root.GetProperty("rows").GetArrayLength()).IsEqualTo(0);
    }

    [Test]
    public async Task Match_MultipleColumns_AllColumnsPresent()
    {
        var bridge = BridgeReturning("""{"rows":[{"name":"Alice","age":30}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync(
            "MATCH (u:User) RETURN u.name AS name, u.age AS age");

        var row = result.Root.GetProperty("rows")[0];
        await Assert.That(row.GetProperty("name").GetString()).IsEqualTo("Alice");
        await Assert.That(row.GetProperty("age").GetInt32()).IsEqualTo(30);
    }

    // ── Parameter usage ──────────────────────────────────────────────

    [Test]
    public async Task Match_WithStringParameter_ParameterInRequestBody()
    {
        var bridge = BridgeReturning("""{"rows":[{"name":"Alice"}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync(
            "MATCH (u:User) WHERE u.name = $name RETURN u.name AS name",
            new Dictionary<string, object?> { ["name"] = "Alice" });

        using var doc = JsonDocument.Parse(bridge.LastRequestJson!);
        await Assert.That(doc.RootElement.GetProperty("params").GetProperty("name").GetString())
            .IsEqualTo("Alice");
    }

    [Test]
    public async Task Match_WithMultipleParameters_AllParametersInRequest()
    {
        var bridge = BridgeReturning("""{"rows":[]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync(
            "MATCH (u:User) WHERE u.name = $name AND u.age >= $minAge RETURN u",
            new Dictionary<string, object?> { ["name"] = "Alice", ["minAge"] = 18 });

        using var doc = JsonDocument.Parse(bridge.LastRequestJson!);
        var prms = doc.RootElement.GetProperty("params");
        await Assert.That(prms.GetProperty("name").GetString()).IsEqualTo("Alice");
        await Assert.That(prms.GetProperty("minAge").GetInt32()).IsEqualTo(18);
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
