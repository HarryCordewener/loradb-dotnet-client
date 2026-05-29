using LoraDb.Client.IntegrationTests.Fixtures;
using TUnit.Core;

namespace LoraDb.Client.IntegrationTests;

public class OrderingPaginationIntegrationTests : IntegrationTestBase
{
    [Test]
    [CombinedDataSources]
    public async Task OrderByAscending_ReturnsAlphabeticalNames(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person) RETURN n.name AS name ORDER BY n.name ASC");
            await AssertStringRowsAsync(result, "name", "Alice", "Bob", "Carol", "Dave");
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task OrderByDescending_ReturnsReverseAlphabeticalNames(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person) RETURN n.name AS name ORDER BY n.name DESC");
            await AssertStringRowsAsync(result, "name", "Dave", "Carol", "Bob", "Alice");
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task OrderByComputedExpression_ReturnsExpectedOrder(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person) RETURN n.name AS name ORDER BY n.age * 2 DESC");
            await AssertStringRowsAsync(result, "name", "Dave", "Bob", "Alice", "Carol");
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task Limit_ReturnsRequestedNumberOfRows(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person) RETURN n.name AS name ORDER BY n.name LIMIT 2");
            await AssertStringRowsAsync(result, "name", "Alice", "Bob");
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task SkipAndLimit_ReturnsRequestedPage(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person) RETURN n.name AS name ORDER BY n.name SKIP 1 LIMIT 2");
            await AssertStringRowsAsync(result, "name", "Bob", "Carol");
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task OrderBySkipAndLimit_CombineCorrectly(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync("MATCH (n:Person) RETURN n.name AS name ORDER BY n.name DESC SKIP 1 LIMIT 2");
            await AssertStringRowsAsync(result, "name", "Carol", "Bob");
        });
    }
}
