using LoraDb.Client.IntegrationTests.Fixtures;
using LoraDb.Client.IntegrationTests.Helpers;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LoraDb.Client.IntegrationTests;

public class ParameterIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task NamedStringParameter_FiltersCorrectly(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (n:Person) WHERE n.name = $name RETURN n.name AS name",
                new Dictionary<string, object?> { ["name"] = "Alice" });
            await AssertSingleStringResult(result, "name", "Alice");
        });
    }

    [Test]
    public async Task NamedIntegerParameter_FiltersAndComputes(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (n:Person) WHERE n.age > $minimum RETURN count(n) AS total, $minimum + 1 AS nextValue",
                new Dictionary<string, object?> { ["minimum"] = 30 });
            await IntegrationAssertions.AssertSingleIntegerResult(result, "total", 2);
            await Assert.That(IntegrationAssertions.GetRowColumn(result, 0, "nextValue").GetInt32()).IsEqualTo(31);
        });
    }

    [Test]
    public async Task NamedFloatParameter_RoundTripsWithoutPrecisionLoss(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "RETURN $value AS value",
                new Dictionary<string, object?> { ["value"] = 3.14159 });
            await Assert.That(IntegrationAssertions.GetRowColumn(result, 0, "value").GetDouble()).IsEqualTo(3.14159);
        });
    }

    [Test]
    public async Task NamedBooleanParameter_FiltersCorrectly(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (n:Person) WHERE n.active = $active RETURN count(n) AS total",
                new Dictionary<string, object?> { ["active"] = true });
            await IntegrationAssertions.AssertSingleIntegerResult(result, "total", 3);
        });
    }

    [Test]
    public async Task NamedParameter_InCreate_IsStoredAndReturned(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "CREATE (n:ParamNode {name: $name}) RETURN n.name AS name",
                new Dictionary<string, object?> { ["name"] = "Stored" });
            await AssertSingleStringResult(result, "name", "Stored");
        });
    }

    [Test]
    public async Task NullParameter_BehavesAsNullInWhereCheck(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "RETURN $value IS NULL AS isNull",
                new Dictionary<string, object?> { ["value"] = null });
            await AssertSingleBooleanResult(result, "isNull", true);
        });
    }

    [Test]
    public async Task MultipleParameters_InSingleQuery_AreAllApplied(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (n:Person) WHERE n.name = $name AND n.age = $age RETURN n.name AS name, n.age AS age",
                new Dictionary<string, object?> { ["name"] = "Bob", ["age"] = 40 });
            await AssertSingleStringResult(result, "name", "Bob");
            await Assert.That(IntegrationAssertions.GetRowColumn(result, 0, "age").GetInt32()).IsEqualTo(40);
        });
    }
}
