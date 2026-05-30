using System.Text.Json;
using System.Text.Json.Serialization;
using LoraDb.Client.IntegrationTests.Fixtures;
using LoraDb.Client.IntegrationTests.Helpers;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LoraDb.Client.IntegrationTests;

public class ResultFormatIntegrationTests : IntegrationTestBase
{
    [Test]
    [CombinedDataSources]
    public async Task RowsFormat_ReturnsNamedRows(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person) RETURN n.name AS name ORDER BY n.name", format: "rows");
            await Assert.That(result.Root.TryGetProperty("rows", out _)).IsTrue();
            await AssertStringRowsAsync(result, "name", "Alice", "Bob", "Carol", "Dave");
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task RowArraysFormat_ReturnsPositionalRows_InHttpMode(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        if (!IsHttpFixture(fixture))
            return;

        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person) RETURN n.name AS name ORDER BY n.name", format: "rowArrays");
            await Assert.That(result.Root.TryGetProperty("columns", out _)).IsTrue();
            await AssertRowArrayStringsAsync(result.Root, "Alice", "Bob", "Carol", "Dave");
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task GraphFormat_ReturnsNodesAndRelationships_InHttpMode(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        if (!IsHttpFixture(fixture))
            return;

        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (a:Person)-[r:FOLLOWS]->(b:Person) RETURN a, r, b",
                format: "graph");
            var graph = result.Root.GetProperty("graph");
            await Assert.That(graph.GetProperty("nodes").GetArrayLength()).IsGreaterThanOrEqualTo(2);
            await Assert.That(graph.GetProperty("relationships").GetArrayLength()).IsGreaterThanOrEqualTo(1);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task CombinedFormat_ReturnsRowsAndGraph_InHttpMode(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        if (!IsHttpFixture(fixture))
            return;

        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (a:Person)-[r:FOLLOWS]->(b:Person) RETURN a.name AS source, r, b.name AS target",
                format: "combined");
            await Assert.That(result.Root.TryGetProperty("data", out _)).IsTrue();
            await Assert.That(result.Root.TryGetProperty("graph", out _)).IsTrue();
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Typed deserialization — ReadRows and ReadRowsEnvelope (both transports)
    // ──────────────────────────────────────────────────────────────────────────

    [Test]
    [CombinedDataSources]
    public async Task RowsFormat_ReadRows_ReturnsTypedRows(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person) RETURN n.name AS name ORDER BY n.name", format: "rows");
            var rows = result.ReadRows<PersonNameRow>();

            await Assert.That(rows.Count).IsEqualTo(4);
            await Assert.That(rows[0].Name).IsEqualTo("Alice");
            await Assert.That(rows[3].Name).IsEqualTo("Dave");
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task RowsFormat_ReadRowsEnvelope_ReturnsTypedEnvelope(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person) RETURN n.name AS name ORDER BY n.name", format: "rows");
            var envelope = result.ReadRowsEnvelope<PersonNameRow>();

            await Assert.That(envelope.Rows.Count).IsEqualTo(4);
            await Assert.That(envelope.Rows[0].Name).IsEqualTo("Alice");
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task RowsFormat_ReadRows_WithTypeInfo_ReturnsTypedRows(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person) RETURN n.name AS name ORDER BY n.name", format: "rows");
            var rows = result.ReadRows(ResultFormatJsonContext.Default.PersonNameRow);

            await Assert.That(rows.Count).IsEqualTo(4);
            await Assert.That(rows[0].Name).IsEqualTo("Alice");
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task RowsFormat_ReadRowsEnvelope_WithTypeInfo_ReturnsTypedEnvelope(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person) RETURN n.name AS name ORDER BY n.name", format: "rows");
            var envelope = result.ReadRowsEnvelope(ResultFormatJsonContext.Default.PersonNameRow);

            await Assert.That(envelope.Rows.Count).IsEqualTo(4);
            await Assert.That(envelope.Rows[0].Name).IsEqualTo("Alice");
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Typed deserialization — ReadRowArrays (HTTP only)
    // ──────────────────────────────────────────────────────────────────────────

    [Test]
    [CombinedDataSources]
    public async Task RowArraysFormat_ReadRowArrays_ReturnsTypedEnvelope_InHttpMode(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        if (!IsHttpFixture(fixture))
            return;

        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (n:Person) RETURN n.name AS name ORDER BY n.name",
                format: "rowArrays");
            var rowArrays = result.ReadRowArrays<string>();

            await Assert.That(rowArrays.Columns).Contains("name");
            await Assert.That(rowArrays.Rows.Count).IsEqualTo(4);
            await Assert.That(rowArrays.Rows[0][0]).IsEqualTo("Alice");
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task RowArraysFormat_ReadRowArrays_WithTypeInfo_ReturnsTypedEnvelope_InHttpMode(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        if (!IsHttpFixture(fixture))
            return;

        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (n:Person) RETURN n.name AS name ORDER BY n.name",
                format: "rowArrays");
            var rowArrays = result.ReadRowArrays(ResultFormatJsonContext.Default.String);

            await Assert.That(rowArrays.Columns).Contains("name");
            await Assert.That(rowArrays.Rows.Count).IsEqualTo(4);
            await Assert.That(rowArrays.Rows[0][0]).IsEqualTo("Alice");
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Typed deserialization — ReadGraph (HTTP only)
    // ──────────────────────────────────────────────────────────────────────────

    [Test]
    [CombinedDataSources]
    public async Task GraphFormat_ReadGraph_ReturnsTypedGraph_InHttpMode(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        if (!IsHttpFixture(fixture))
            return;

        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (a:Person)-[r:FOLLOWS]->(b:Person) RETURN a, r, b",
                format: "graph");
            var graph = result.ReadGraph<GraphNode, GraphRelationship>();

            await Assert.That(graph.Graph.Nodes.Count).IsGreaterThanOrEqualTo(2);
            await Assert.That(graph.Graph.Relationships.Count).IsGreaterThanOrEqualTo(1);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task GraphFormat_ReadGraph_WithTypeInfo_ReturnsTypedGraph_InHttpMode(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        if (!IsHttpFixture(fixture))
            return;

        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (a:Person)-[r:FOLLOWS]->(b:Person) RETURN a, r, b",
                format: "graph");
            var graph = result.ReadGraph(
                ResultFormatJsonContext.Default.GraphNode,
                ResultFormatJsonContext.Default.GraphRelationship);

            await Assert.That(graph.Graph.Nodes.Count).IsGreaterThanOrEqualTo(2);
            await Assert.That(graph.Graph.Relationships.Count).IsGreaterThanOrEqualTo(1);
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Typed deserialization — ReadCombined (HTTP only)
    // ──────────────────────────────────────────────────────────────────────────

    [Test]
    [CombinedDataSources]
    public async Task CombinedFormat_ReadCombined_ReturnsTypedResult_InHttpMode(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        if (!IsHttpFixture(fixture))
            return;

        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (a:Person)-[r:FOLLOWS]->(b:Person) RETURN a.name AS source, r, b.name AS target",
                format: "combined");
            var combined = result.ReadCombined<RelationshipData, GraphNode, GraphRelationship>();

            await Assert.That(combined.Columns).Contains("source");
            await Assert.That(combined.Data.Count).IsGreaterThanOrEqualTo(1);
            await Assert.That(combined.Graph.Nodes.Count).IsGreaterThanOrEqualTo(2);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task CombinedFormat_ReadCombined_WithTypeInfo_ReturnsTypedResult_InHttpMode(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        if (!IsHttpFixture(fixture))
            return;

        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (a:Person)-[r:FOLLOWS]->(b:Person) RETURN a.name AS source, r, b.name AS target",
                format: "combined");
            var combined = result.ReadCombined(
                ResultFormatJsonContext.Default.RelationshipData,
                ResultFormatJsonContext.Default.GraphNode,
                ResultFormatJsonContext.Default.GraphRelationship);

            await Assert.That(combined.Columns).Contains("source");
            await Assert.That(combined.Data.Count).IsGreaterThanOrEqualTo(1);
            await Assert.That(combined.Graph.Nodes.Count).IsGreaterThanOrEqualTo(2);
        });
    }
}

// Model types used by typed deserialization integration tests.

public sealed class PersonNameRow
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

/// <summary>Minimal graph node; unknown server fields are ignored by STJ.</summary>
public sealed class GraphNode { }

/// <summary>Minimal graph relationship; unknown server fields are ignored by STJ.</summary>
public sealed class GraphRelationship { }

public sealed class RelationshipData
{
    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; init; } = string.Empty;
}

[JsonSerializable(typeof(PersonNameRow))]
[JsonSerializable(typeof(GraphNode))]
[JsonSerializable(typeof(GraphRelationship))]
[JsonSerializable(typeof(RelationshipData))]
[JsonSerializable(typeof(string))]
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
internal partial class ResultFormatJsonContext : JsonSerializerContext;
