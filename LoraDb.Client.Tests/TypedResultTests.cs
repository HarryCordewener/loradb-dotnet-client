using System.Text.Json;
using System.Text.Json.Serialization;
using LoraDb.Client.Tests.Helpers;
using TUnit.Assertions.Extensions;

namespace LoraDb.Client.Tests;

public class TypedResultTests
{
    private static readonly Uri Endpoint = new("http://localhost:4747/");

    // ──────────────────────────────────────────────────────────────────────────
    // Deserialize<T> — root-document overloads
    // ──────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Deserialize_ReturnsTypedRoot()
    {
        var bridge = new FakeNativeBridge("""{"name":"Alice"}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("RETURN 1");
        var row = result.Deserialize<PersonRow>();

        await Assert.That(row.Name).IsEqualTo("Alice");
    }

    [Test]
    public async Task Deserialize_WithCustomOptions_ReturnsTypedRoot()
    {
        var bridge = new FakeNativeBridge("""{"name":"Alice"}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("RETURN 1");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var row = result.Deserialize<PersonRow>(options);

        await Assert.That(row.Name).IsEqualTo("Alice");
    }

    [Test]
    public async Task Deserialize_NullOptions_ThrowsArgumentNullException()
    {
        var bridge = new FakeNativeBridge("""{"name":"Alice"}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("RETURN 1");

        await Assert.That(() => result.Deserialize<PersonRow>(null!))
            .ThrowsException()
            .And
            .IsTypeOf<ArgumentNullException>();
    }

    [Test]
    public async Task Deserialize_WhenDeserializationFails_ThrowsInvalidOperationException()
    {
        // Root is a JSON object; attempting to deserialize it as a plain int must fail gracefully.
        var bridge = new FakeNativeBridge("""{"name":"Alice"}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("RETURN 1");

        await Assert.That(() => result.Deserialize<int>())
            .ThrowsException()
            .And
            .IsTypeOf<InvalidOperationException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ReadRowsEnvelope — generic and TypeInfo overloads
    // ──────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task ReadRowsEnvelope_ReturnsEnvelope()
    {
        var bridge = new FakeNativeBridge("""{"rows":[{"name":"Alice"},{"name":"Bob"}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("MATCH (n) RETURN n.name AS name");
        var envelope = result.ReadRowsEnvelope<PersonRow>();

        await Assert.That(envelope.Rows.Count).IsEqualTo(2);
        await Assert.That(envelope.Rows[0].Name).IsEqualTo("Alice");
        await Assert.That(envelope.Rows[1].Name).IsEqualTo("Bob");
    }

    [Test]
    public async Task ReadRowsEnvelope_WithTypeInfo_ReturnsEnvelope()
    {
        var bridge = new FakeNativeBridge("""{"rows":[{"name":"Alice"}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("MATCH (n) RETURN n.name AS name");
        var envelope = result.ReadRowsEnvelope(TypedResultJsonContext.Default.PersonRow);

        await Assert.That(envelope.Rows.Count).IsEqualTo(1);
        await Assert.That(envelope.Rows[0].Name).IsEqualTo("Alice");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ReadRows — existing generic overload (kept) + TypeInfo overload
    // ──────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Embedded_ReadRows_ReturnsTypedRows()
    {
        var bridge = new FakeNativeBridge("""{"rows":[{"name":"Alice"},{"name":"Bob"}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("MATCH (n) RETURN n.name AS name");
        var rows = result.ReadRows<PersonRow>();

        await Assert.That(rows.Count).IsEqualTo(2);
        await Assert.That(rows[0].Name).IsEqualTo("Alice");
        await Assert.That(rows[1].Name).IsEqualTo("Bob");
    }

    [Test]
    public async Task Embedded_ReadRows_WithTypeInfo_ReturnsTypedRows()
    {
        var bridge = new FakeNativeBridge("""{"rows":[{"name":"Alice"}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("MATCH (n) RETURN n.name AS name");
        var rows = result.ReadRows(TypedResultJsonContext.Default.PersonRow);

        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].Name).IsEqualTo("Alice");
    }

    [Test]
    public async Task Http_ReadRowArrays_ReturnsTypedEnvelope()
    {
        var handler = RecordingHttpHandler.WithJson("""{"columns":["a","b"],"rows":[[1,2],[3,4]]}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync("RETURN 1 AS a, 2 AS b", format: "rowArrays");
        var rowArrays = result.ReadRowArrays<int>();

        await Assert.That(rowArrays.Columns.Count).IsEqualTo(2);
        await Assert.That(rowArrays.Columns[0]).IsEqualTo("a");
        await Assert.That(rowArrays.Rows[1][1]).IsEqualTo(4);
    }

    [Test]
    public async Task Http_ReadGraph_ReturnsTypedEnvelope()
    {
        var handler = RecordingHttpHandler.WithJson(
            """{"graph":{"nodes":[{"id":1},{"id":2}],"relationships":[{"id":5,"type":"FOLLOWS"}]}}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync("MATCH (a)-[r]->(b) RETURN a,r,b", format: "graph");
        var graph = result.ReadGraph<GraphNode, GraphRelationship>();

        await Assert.That(graph.Graph.Nodes.Count).IsEqualTo(2);
        await Assert.That(graph.Graph.Relationships.Count).IsEqualTo(1);
        await Assert.That(graph.Graph.Relationships[0].Type).IsEqualTo("FOLLOWS");
    }

    [Test]
    public async Task Http_ReadCombined_ReturnsTypedEnvelope()
    {
        var handler = RecordingHttpHandler.WithJson(
            """{"columns":["source"],"data":[{"source":"Alice"}],"graph":{"nodes":[{"id":1}],"relationships":[{"id":2,"type":"FOLLOWS"}]}}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync("MATCH ...", format: "combined");
        var combined = result.ReadCombined<CombinedData, GraphNode, GraphRelationship>();

        await Assert.That(combined.Columns[0]).IsEqualTo("source");
        await Assert.That(combined.Data[0].Source).IsEqualTo("Alice");
        await Assert.That(combined.Graph.Nodes[0].Id).IsEqualTo(1);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ReadRowArrays — TypeInfo overload
    // ──────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Http_ReadRowArrays_WithTypeInfo_ReturnsTypedEnvelope()
    {
        var handler = RecordingHttpHandler.WithJson("""{"columns":["a","b"],"rows":[[1,2],[3,4]]}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync("RETURN 1 AS a, 2 AS b", format: "rowArrays");
        var rowArrays = result.ReadRowArrays(TypedResultJsonContext.Default.Int32);

        await Assert.That(rowArrays.Columns.Count).IsEqualTo(2);
        await Assert.That(rowArrays.Columns[0]).IsEqualTo("a");
        await Assert.That(rowArrays.Rows[1][1]).IsEqualTo(4);
    }

    [Test]
    public async Task ReadRowArrays_WithTypeInfo_ThrowsWhenRowsNotAnArray()
    {
        // The TypeInfo overload validates that "rows" is a JSON array.
        var bridge = new FakeNativeBridge("""{"columns":["a"],"rows":"not-an-array"}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("RETURN 1 AS a", format: "rowArrays");

        await Assert.That(() => result.ReadRowArrays(TypedResultJsonContext.Default.Int32))
            .ThrowsException()
            .And
            .IsTypeOf<InvalidOperationException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ReadGraph — TypeInfo overload
    // ──────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Http_ReadGraph_WithTypeInfo_ReturnsTypedEnvelope()
    {
        var handler = RecordingHttpHandler.WithJson(
            """{"graph":{"nodes":[{"id":1},{"id":2}],"relationships":[{"id":5,"type":"FOLLOWS"}]}}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync("MATCH (a)-[r]->(b) RETURN a,r,b", format: "graph");
        var graph = result.ReadGraph(
            TypedResultJsonContext.Default.GraphNode,
            TypedResultJsonContext.Default.GraphRelationship);

        await Assert.That(graph.Graph.Nodes.Count).IsEqualTo(2);
        await Assert.That(graph.Graph.Relationships.Count).IsEqualTo(1);
        await Assert.That(graph.Graph.Relationships[0].Type).IsEqualTo("FOLLOWS");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ReadCombined — TypeInfo overload
    // ──────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Http_ReadCombined_WithTypeInfo_ReturnsTypedEnvelope()
    {
        var handler = RecordingHttpHandler.WithJson(
            """{"columns":["source"],"data":[{"source":"Alice"}],"graph":{"nodes":[{"id":1}],"relationships":[{"id":2,"type":"FOLLOWS"}]}}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync("MATCH ...", format: "combined");
        var combined = result.ReadCombined(
            TypedResultJsonContext.Default.CombinedData,
            TypedResultJsonContext.Default.GraphNode,
            TypedResultJsonContext.Default.GraphRelationship);

        await Assert.That(combined.Columns[0]).IsEqualTo("source");
        await Assert.That(combined.Data[0].Source).IsEqualTo("Alice");
        await Assert.That(combined.Graph.Nodes[0].Id).IsEqualTo(1);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Error cases
    // ──────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task ReadRows_ThrowsWhenRowsMissing()
    {
        var bridge = new FakeNativeBridge("""{"graph":{"nodes":[],"relationships":[]}}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("MATCH (n) RETURN n", format: "graph");

        await Assert.That(() => result.ReadRows<PersonRow>())
            .ThrowsException()
            .And
            .IsTypeOf<InvalidOperationException>();
    }

    [Test]
    public async Task ExecuteRowsAsync_ReturnsTypedRows()
    {
        var bridge = new FakeNativeBridge("""{"rows":[{"name":"Alice"}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var rows = await client.ExecuteRowsAsync<PersonRow>("MATCH (n) RETURN n.name AS name");

        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].Name).IsEqualTo("Alice");
    }

    [Test]
    public async Task ExecuteRowsAsync_WithTypeInfo_ReturnsTypedRows()
    {
        var bridge = new FakeNativeBridge("""{"rows":[{"name":"Alice"}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var rows = await client.ExecuteRowsAsync("MATCH (n) RETURN n.name AS name", TypedResultJsonContext.Default.PersonRow);

        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].Name).IsEqualTo("Alice");
    }

    public sealed class PersonRow
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;
    }

    public sealed class CombinedData
    {
        [JsonPropertyName("source")]
        public string Source { get; init; } = string.Empty;
    }

    public sealed class GraphNode
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }
    }

    public sealed class GraphRelationship
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;
    }
}

[JsonSerializable(typeof(TypedResultTests.PersonRow))]
[JsonSerializable(typeof(TypedResultTests.GraphNode))]
[JsonSerializable(typeof(TypedResultTests.GraphRelationship))]
[JsonSerializable(typeof(TypedResultTests.CombinedData))]
[JsonSerializable(typeof(int))]
internal partial class TypedResultJsonContext : JsonSerializerContext;
