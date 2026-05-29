using LoraDb.Client.IntegrationTests.Fixtures;
using LoraDb.Client.IntegrationTests.Helpers;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LoraDb.Client.IntegrationTests;

public class ResultFormatIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task RowsFormat_ReturnsNamedRows(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person) RETURN n.name AS name ORDER BY name", format: "rows");
            await Assert.That(result.Root.TryGetProperty("rows", out _)).IsTrue();
            await AssertStringRowsAsync(result, "name", "Alice", "Bob", "Carol", "Dave");
        });
    }

    [Test]
    public async Task RowArraysFormat_ReturnsPositionalRows_InHttpMode(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        if (!IsHttpFixture(fixture))
            return;

        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person) RETURN n.name AS name ORDER BY name", format: "rowArrays");
            await Assert.That(result.Root.TryGetProperty("rowArrays", out _)).IsTrue();
            await AssertRowArrayStringsAsync(result.Root, "Alice", "Bob", "Carol", "Dave");
        });
    }

    [Test]
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
            await Assert.That(result.Root.TryGetProperty("rows", out _)).IsTrue();
            await Assert.That(result.Root.TryGetProperty("graph", out _)).IsTrue();
        });
    }
}
