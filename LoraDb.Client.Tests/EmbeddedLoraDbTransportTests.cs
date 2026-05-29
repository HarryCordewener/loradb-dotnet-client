using System.Text.Json;
using LoraDb.Client.Native;

namespace LoraDb.Client.Tests;

public class EmbeddedLoraDbTransportTests
{
    [Fact]
    public async Task ExecuteAsync_UsesNativeBridgeForQueryExecution()
    {
        var bridge = new FakeNativeBridge();

        await using var client = LoraDbClient.CreateEmbedded(bridge);
        using var result = await client.ExecuteAsync(
            "MATCH (u:User) RETURN u.name AS name",
            new Dictionary<string, object?> { ["limit"] = 1 });

        Assert.Equal("Alice", result.Root.GetProperty("rows")[0].GetProperty("name").GetString());

        Assert.NotNull(bridge.LastRequestJson);
        using var payloadJson = JsonDocument.Parse(bridge.LastRequestJson!);
        Assert.Equal(1, payloadJson.RootElement.GetProperty("params").GetProperty("limit").GetInt32());
    }

    [Fact]
    public async Task ExecuteAsync_RejectsEmptyQuery()
    {
        var bridge = new FakeNativeBridge();

        await using var client = LoraDbClient.CreateEmbedded(bridge);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => client.ExecuteAsync("  "));
        Assert.Equal("query", ex.ParamName);
    }

    private sealed class FakeNativeBridge : ILoraDbNativeBridge
    {
        public string? LastRequestJson { get; private set; }

        public string ExecuteJson(string requestJson)
        {
            LastRequestJson = requestJson;
            return "{\"rows\":[{\"name\":\"Alice\"}]}";
        }

        public void Dispose()
        {
        }
    }
}
