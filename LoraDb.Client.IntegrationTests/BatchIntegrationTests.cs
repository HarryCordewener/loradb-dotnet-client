using LoraDb.Client.IntegrationTests.Fixtures;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LoraDb.Client.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="LoraDbBatch"/>.
/// Verifies that sequential batch execution works correctly against a real LoraDB instance.
/// </summary>
public class BatchIntegrationTests : IntegrationTestBase
{
    [Test]
    [CombinedDataSources]
    public async Task Batch_ExecutesAllStatementsInOrder(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var keyA = UniqueValue("batch-order");
            var keyB = UniqueValue("batch-order");

            using var batchResult = await client.CreateBatch()
                .Add($"CREATE (:BatchOrder {{key: '{keyA}', seq: 1}})")
                .Add($"CREATE (:BatchOrder {{key: '{keyB}', seq: 2}})")
                .ExecuteAsync();

            await Assert.That(batchResult.Results.Count).IsEqualTo(2);

            using var countResult = await client.ExecuteAsync(
                $"MATCH (n:BatchOrder) WHERE n.key IN ['{keyA}', '{keyB}'] RETURN count(n) AS total");
            await Helpers.IntegrationAssertions.AssertSingleIntegerResult(countResult, "total", 2);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task Batch_EmptyBatch_ReturnsEmptyResults(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            using var batchResult = await client.CreateBatch().ExecuteAsync();

            await Assert.That(batchResult.Results.Count).IsEqualTo(0);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task Batch_EachResultContainsCorrectPayload(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var keyA = UniqueValue("batch-payload");
            var keyB = UniqueValue("batch-payload");
            using var create = await client.ExecuteAsync(
                $"CREATE (:BatchPayload {{key: '{keyA}', val: 10}}), (:BatchPayload {{key: '{keyB}', val: 20}})");

            using var batchResult = await client.CreateBatch()
                .Add($"MATCH (n:BatchPayload {{key: '{keyA}'}}) RETURN n.val AS val")
                .Add($"MATCH (n:BatchPayload {{key: '{keyB}'}}) RETURN n.val AS val")
                .ExecuteAsync();

            await Assert.That(batchResult.Results.Count).IsEqualTo(2);
            await Helpers.IntegrationAssertions.AssertSingleIntegerResult(batchResult.Results[0], "val", 10);
            await Helpers.IntegrationAssertions.AssertSingleIntegerResult(batchResult.Results[1], "val", 20);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task Batch_WithParameters_PassesParametersCorrectly(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var key = UniqueValue("batch-param");

            using var batchResult = await client.CreateBatch()
                .Add("CREATE (n:BatchParam {key: $key})", new Dictionary<string, object?> { ["key"] = key })
                .ExecuteAsync();

            await Assert.That(batchResult.Results.Count).IsEqualTo(1);

            using var verify = await client.ExecuteAsync(
                "MATCH (n:BatchParam {key: $key}) RETURN count(n) AS total",
                new Dictionary<string, object?> { ["key"] = key });
            await Helpers.IntegrationAssertions.AssertSingleIntegerResult(verify, "total", 1);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task Batch_StopsOnFirstError_DoesNotExecuteSubsequentStatements(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var key = UniqueValue("batch-failfast");

            await Assert.That(async () =>
                await client.CreateBatch()
                    .Add("THIS IS NOT VALID CYPHER @@@@")
                    .Add($"CREATE (:FailFastNode {{key: '{key}'}})")
                    .ExecuteAsync())
                .ThrowsException();

            // The second statement must not have run because the first failed
            using var countResult = await client.ExecuteAsync(
                $"MATCH (n:FailFastNode {{key: '{key}'}}) RETURN count(n) AS total");
            await Helpers.IntegrationAssertions.AssertSingleIntegerResult(countResult, "total", 0);
        });
    }
}
