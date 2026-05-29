using LoraDb.Client.IntegrationTests.Fixtures;
using LoraDb.Client.IntegrationTests.Helpers;
using TUnit.Core;

namespace LoraDb.Client.IntegrationTests;

public class MatchIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task Match_AllNodes_ReturnsSeededCount(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n) RETURN count(n) AS total");
            await IntegrationAssertions.AssertSingleIntegerResult(result, "total", 4);
        });
    }

    [Test]
    public async Task Match_BySingleLabel_ReturnsMatchingNodesOnly(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithGraphsAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Product) RETURN n.name AS name ORDER BY name");
            await AssertStringRowsAsync(result, "name", "Coffee", "Keyboard", "Mouse", "Tea");
        });
    }

    [Test]
    public async Task Match_WithWherePropertyEquality_ReturnsSingleNode(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person) WHERE n.name = 'Alice' RETURN n.name AS name");
            await AssertSingleStringResult(result, "name", "Alice");
        });
    }

    [Test]
    public async Task Match_ByRelationshipTypeAndDirection_ReturnsCorrectPair(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (a:Person)-[:FOLLOWS]->(b:Person) RETURN a.name AS source, b.name AS target");
            await AssertSingleStringResult(result, "source", "Alice");
            await AssertSingleStringResult(result, "target", "Bob");
        });
    }

    [Test]
    public async Task Match_WithOptionalMatch_IncludesNodesWithoutRelationships(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (p:Person) OPTIONAL MATCH (p)-[:FOLLOWS]->(f:Person) RETURN p.name AS person, f.name AS follows ORDER BY person");
            await IntegrationAssertions.AssertRowCount(result, 4);
        });
    }

    [Test]
    public async Task Match_MultiHopTraversal_ReturnsExpectedEndpoint(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (:Person {name: 'Alice'})-[*2]->(target:Person) RETURN target.name AS name");
            await AssertSingleStringResult(result, "name", "Carol");
        });
    }

    [Test]
    public async Task Match_WithCrossProduct_ReturnsDisconnectedCombinations(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (a:Person {name: 'Alice'}) MATCH (b:Person) RETURN a.name AS first, b.name AS second ORDER BY second");
            await IntegrationAssertions.AssertRowCount(result, 4);
        });
    }
}
