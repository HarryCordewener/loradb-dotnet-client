using LoraDb.Client.IntegrationTests.Fixtures;
using LoraDb.Client.IntegrationTests.Helpers;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LoraDb.Client.IntegrationTests;

public class AggregationIntegrationTests : IntegrationTestBase
{
    [Test]
    [CombinedDataSources]
    public async Task Count_ReturnsSeededNodeCount(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person) RETURN count(n) AS total");
            await IntegrationAssertions.AssertSingleIntegerResult(result, "total", 4);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task Sum_ReturnsCorrectTotal(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person) RETURN sum(n.age) AS total");
            await IntegrationAssertions.AssertSingleIntegerResult(result, "total", 140);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task Average_ReturnsCorrectValue(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person) RETURN avg(n.age) AS average");
            await Assert.That(IntegrationAssertions.GetRowColumn(result, 0, "average").GetDouble()).IsEqualTo(35.0);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task MinAndMax_ReturnCorrectBoundaries(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person) RETURN min(n.age) AS minimum, max(n.age) AS maximum");
            await Assert.That(IntegrationAssertions.GetRowColumn(result, 0, "minimum").GetInt32()).IsEqualTo(25);
            await Assert.That(IntegrationAssertions.GetRowColumn(result, 0, "maximum").GetInt32()).IsEqualTo(45);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task Collect_ReturnsAllNames(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (n:Person) WITH n ORDER BY n.name RETURN collect(n.name) AS names");
            var names = IntegrationAssertions.GetRowColumn(result, 0, "names");
            await Assert.That(names.GetArrayLength()).IsEqualTo(4);
            await Assert.That(names[0].GetString()).IsEqualTo("Alice");
            await Assert.That(names[3].GetString()).IsEqualTo("Dave");
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task GroupedAggregation_ReturnsCountsPerCategory(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithProductGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (p:Product) RETURN p.category AS label, count(p) AS total ORDER BY label");
            await IntegrationAssertions.AssertRowCount(result, 2);
            await Assert.That(IntegrationAssertions.GetRowColumn(result, 0, "label").GetString()).IsEqualTo("Grocery");
            await Assert.That(IntegrationAssertions.GetRowColumn(result, 0, "total").GetInt32()).IsEqualTo(2);
            await Assert.That(IntegrationAssertions.GetRowColumn(result, 1, "label").GetString()).IsEqualTo("Hardware");
            await Assert.That(IntegrationAssertions.GetRowColumn(result, 1, "total").GetInt32()).IsEqualTo(2);
        });
    }
}
