using LoraDb.Client.IntegrationTests.Fixtures;
using LoraDb.Client.IntegrationTests.Helpers;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LoraDb.Client.IntegrationTests;

public class ExpressionIntegrationTests : IntegrationTestBase
{
    [Test]
    [CombinedDataSources]
    public async Task ArithmeticExpression_ReturnsExpectedValue(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("RETURN 2 + 3 AS r");
            await IntegrationAssertions.AssertSingleIntegerResult(result, "r", 5);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task StringConcatenation_ReturnsCombinedString(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("RETURN 'Hello' + ' World' AS r");
            await AssertSingleStringResult(result, "r", "Hello World");
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task BooleanLogic_ReturnsExpectedValue(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("RETURN true AND false AS r");
            await AssertSingleBooleanResult(result, "r", false);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task CaseExpression_PicksCorrectBranch(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("RETURN CASE WHEN 2 > 1 THEN 'yes' ELSE 'no' END AS r");
            await AssertSingleStringResult(result, "r", "yes");
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task UnwindList_ExpandsToMultipleRows(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("UNWIND ['Alice', 'Bob', 'Carol'] AS name RETURN name ORDER BY name");
            await AssertStringRowsAsync(result, "name", "Alice", "Bob", "Carol");
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task ListIndexing_OnProperty_ReturnsExpectedElement(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person {name: 'Alice'}) RETURN n.tags[1] AS r");
            await AssertSingleStringResult(result, "r", "alpha");
        });
    }
}
