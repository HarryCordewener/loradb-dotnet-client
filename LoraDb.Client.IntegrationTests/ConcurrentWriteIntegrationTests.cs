using LoraDb.Client.IntegrationTests.Fixtures;
using LoraDb.Client.IntegrationTests.Helpers;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LoraDb.Client.IntegrationTests;

/// <summary>
/// Aggressively exercises write-write conflicts by firing many concurrent writes at
/// overlapping or identical graph data.  These tests verify that LoraDB's write
/// serialisation keeps the store consistent under pressure.
/// </summary>
public class ConcurrentWriteIntegrationTests : IntegrationTestBase
{
    private const int Workers = 20;

    /// <summary>
    /// Many clients concurrently MERGE the same node.  Because MERGE is idempotent
    /// and writes serialise, exactly one node must exist after all writes complete —
    /// no phantom duplicates, no exceptions.
    /// </summary>
    [Test]
    [CombinedDataSources]
    public async Task ConcurrentMerge_SameNode_ExactlyOneNodeExists(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async _ =>
        {
            var key = UniqueValue("merge");

            var tasks = Enumerable.Range(0, Workers).Select(async _ =>
            {
                await using var client = fixture.CreateClient();
                using var result = await client.ExecuteAsync(
                    $"MERGE (n:ConcurrentMerge {{key: '{key}'}}) RETURN n");
            }).ToArray();

            await Task.WhenAll(tasks);

            await using var verifyClient = fixture.CreateClient();
            using var countResult = await verifyClient.ExecuteAsync(
                $"MATCH (n:ConcurrentMerge {{key: '{key}'}}) RETURN count(n) AS total");
            await IntegrationAssertions.AssertSingleIntegerResult(countResult, "total", 1);
        });
    }

    /// <summary>
    /// Many clients each CREATE a node with a distinct key, all fired in parallel.
    /// No write should be silently dropped: the final count must equal the worker
    /// count, proving write serialisation incurs no data loss.
    /// </summary>
    [Test]
    [CombinedDataSources]
    public async Task ConcurrentCreate_UniqueNodes_NoWritesLost(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async _ =>
        {
            var tag = UniqueValue("batch");
            var keys = Enumerable.Range(0, Workers).Select(i => $"{tag}-{i}").ToArray();

            var tasks = keys.Select(async key =>
            {
                await using var client = fixture.CreateClient();
                using var result = await client.ExecuteAsync(
                    $"CREATE (:ConcurrentCreate {{tag: '{tag}', key: '{key}'}})");
            }).ToArray();

            await Task.WhenAll(tasks);

            await using var verifyClient = fixture.CreateClient();
            using var countResult = await verifyClient.ExecuteAsync(
                $"MATCH (n:ConcurrentCreate {{tag: '{tag}'}}) RETURN count(n) AS total");
            await IntegrationAssertions.AssertSingleIntegerResult(countResult, "total", Workers);
        });
    }

    /// <summary>
    /// Many clients race to CREATE a node that shares a unique-constrained key.
    /// Exactly one write must win; the others must fail with a constraint violation
    /// and not leave partial data behind.  After all tasks finish the node count
    /// must be exactly 1.
    /// </summary>
    [Test]
    [CombinedDataSources]
    public async Task ConcurrentCreate_SameConstrainedKey_ExactlyOneWins(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        const string constraintName = "concwrite_key_unique";

        await WithCleanDatabaseAsync(fixture, async _ =>
        {
            var key = UniqueValue("race");

            await using var setupClient = fixture.CreateClient();
            await setupClient.ExecuteAsync($"DROP CONSTRAINT {constraintName} IF EXISTS");
            await setupClient.ExecuteAsync(
                $"CREATE CONSTRAINT {constraintName} FOR (n:ConcurrentRace) REQUIRE n.key IS UNIQUE");

            try
            {
                var tasks = Enumerable.Range(0, Workers).Select(async _ =>
                {
                    await using var client = fixture.CreateClient();
                    try
                    {
                        using var result = await client.ExecuteAsync(
                            $"CREATE (:ConcurrentRace {{key: '{key}'}})");
                        return true;
                    }
                    catch
                    {
                        // Constraint violation: expected for all but the winning writer.
                        return false;
                    }
                }).ToArray();

                var outcomes = await Task.WhenAll(tasks);
                var successCount = outcomes.Count(won => won);

                await Assert.That(successCount).IsEqualTo(1)
                    .Because("exactly one concurrent write should win the constraint race");

                await using var verifyClient = fixture.CreateClient();
                using var countResult = await verifyClient.ExecuteAsync(
                    $"MATCH (n:ConcurrentRace {{key: '{key}'}}) RETURN count(n) AS total");
                await IntegrationAssertions.AssertSingleIntegerResult(countResult, "total", 1);
            }
            finally
            {
                await using var cleanupClient = fixture.CreateClient();
                await cleanupClient.ExecuteAsync($"DROP CONSTRAINT {constraintName} IF EXISTS");
            }
        });
    }
}
