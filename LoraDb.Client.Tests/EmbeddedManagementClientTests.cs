using LoraDb.Client.Tests.Helpers;
using TUnit.Assertions.Extensions;

namespace LoraDb.Client.Tests;

public class EmbeddedManagementClientTests
{
    [Test]
    public async Task ExplainAsync_UsesNativeBridge()
    {
        var bridge = new FakeNativeBridge(_ => """{"query":"MATCH (n) RETURN n","shape":"readOnly","resultColumns":[],"tree":{"id":0,"operator":"Projection","details":{},"estimatedRows":null,"children":[]}}""");
        await using var client = LoraDbEmbeddedManagementClient.Create(bridge);

        var plan = await client.ExplainAsync("MATCH (n) RETURN n");

        await Assert.That(plan.Shape).IsEqualTo("readOnly");
    }

    [Test]
    public async Task SaveSnapshotAsync_UsesPath()
    {
        var bridge = new FakeNativeBridge();
        await using var client = LoraDbEmbeddedManagementClient.Create(bridge);

        var meta = await client.SaveSnapshotAsync("/tmp/test.snapshot");

        await Assert.That(meta.Path).IsEqualTo("/tmp/test.snapshot");
    }
}
