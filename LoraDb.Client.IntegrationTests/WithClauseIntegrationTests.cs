using LoraDb.Client.IntegrationTests.Fixtures;
using LoraDb.Client.IntegrationTests.Helpers;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LoraDb.Client.IntegrationTests;

public class WithClauseIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task With_PassesVariableToNextClause(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person) WITH n RETURN count(n) AS total");
            await IntegrationAssertions.AssertSingleIntegerResult(result, "total", 4);
        });
    }

    [Test]
    public async Task With_Aggregation_PipesIntoReturn(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person) WITH avg(n.age) AS average RETURN average");
            await Assert.That(IntegrationAssertions.GetRowColumn(result, 0, "average").GetDouble()).IsEqualTo(35.0);
        });
    }

    [Test]
    public async Task WithAndWhere_FiltersAfterAggregation(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (n:Person) WITH n.active AS active, count(*) AS total WHERE total >= 3 RETURN active, total ORDER BY active");
            await IntegrationAssertions.AssertRowCount(result, 2);
        });
    }

    [Test]
    public async Task WithOrderByAndSkip_PagesBeforeSecondMatch(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (n:Person) WITH n ORDER BY n.age DESC SKIP 1 LIMIT 1 MATCH (n)-[:KNOWS]->(m) RETURN n.name AS source, m.name AS target");
            await AssertSingleStringResult(result, "source", "Bob");
            await AssertSingleStringResult(result, "target", "Carol");
        });
    }
}
