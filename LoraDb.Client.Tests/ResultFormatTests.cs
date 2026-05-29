using System.Text.Json;
using LoraDb.Client.Tests.Helpers;
using TUnit.Assertions.Extensions;

namespace LoraDb.Client.Tests;

/// <summary>
/// Tests for result format handling: rows, rowArrays, graph, combined.
/// Mirrors the format coverage in crates/lora-server/tests/http.rs.
/// </summary>
public class ResultFormatTests
{
    private static readonly Uri Endpoint = new("http://localhost:4747/");

    [Test]
    [Arguments("rows")]
    [Arguments("rowArrays")]
    [Arguments("graph")]
    [Arguments("combined")]
    public async Task Http_AllResultFormats_IncludedInRequestBody(string format)
    {
        var handler = RecordingHttpHandler.WithJson($$"""{"{{format}}":[]}""");
        await using var client = LoraDbClient.CreateHttp(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync(
            "MATCH (n:User) RETURN n.name AS name",
            format: format);

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        await Assert.That(doc.RootElement.GetProperty("format").GetString()).IsEqualTo(format);
    }

    [Test]
    public async Task Embedded_FormatDefaultsToRows()
    {
        var bridge = new FakeNativeBridge();
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("MATCH (n) RETURN n");

        using var doc = JsonDocument.Parse(bridge.LastRequestJson!);
        await Assert.That(doc.RootElement.GetProperty("format").GetString()).IsEqualTo("rows");
    }

    [Test]
    public async Task Embedded_CustomFormat_IncludedInRequest()
    {
        var bridge = new FakeNativeBridge("""{"graph":{}}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("MATCH (n) RETURN n", format: "graph");

        using var doc = JsonDocument.Parse(bridge.LastRequestJson!);
        await Assert.That(doc.RootElement.GetProperty("format").GetString()).IsEqualTo("graph");
    }

    [Test]
    public async Task Result_Root_ExposesRawJsonDocument()
    {
        var bridge = new FakeNativeBridge("""{"rows":[{"id":1},{"id":2}]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        using var result = await client.ExecuteAsync("MATCH (n) RETURN n");

        await Assert.That(result.Root.GetProperty("rows").GetArrayLength()).IsEqualTo(2);
    }

    [Test]
    public async Task Result_Dispose_DoesNotThrow()
    {
        var bridge = new FakeNativeBridge("""{"rows":[]}""");
        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var result = await client.ExecuteAsync("MATCH (n) RETURN n");
        result.Dispose();

        // Calling Dispose twice must be safe (IDisposable contract)
        result.Dispose();
    }
}
