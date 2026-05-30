using System.Text.Json.Serialization;
using LoraDb.Client.Tests.Helpers;
using TUnit.Assertions.Extensions;

namespace LoraDb.Client.Tests;

public class TypedResultTests
{
    private static readonly Uri Endpoint = new("http://localhost:4747/");

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
internal partial class TypedResultJsonContext : JsonSerializerContext;
