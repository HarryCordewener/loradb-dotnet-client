using LoraDb.Client.IntegrationTests.Fixtures;
using LoraDb.Client.IntegrationTests.Helpers;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LoraDb.Client.IntegrationTests;

public class TransactionIntegrationTests : IntegrationTestBase
{
    [Test]
    [CombinedDataSources]
    public async Task AutoCommit_CommittedWrite_IsVisibleToSecondClient(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var key = UniqueValue("txvisible");

            using var createResult = await client.ExecuteAsync($"CREATE (:TxVisible {{key: '{key}'}})");

            await using var secondClient = fixture.CreateClient();
            using var matchResult = await secondClient.ExecuteAsync(
                $"MATCH (n:TxVisible {{key: '{key}'}}) RETURN n.key AS key");
            await AssertSingleStringResult(matchResult, "key", key);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task AutoCommit_MultipleWritesInSingleQuery_AllCommitAtomically(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var keyA = UniqueValue("txatomic");
            var keyB = UniqueValue("txatomic");
            var keyC = UniqueValue("txatomic");

            using var createResult = await client.ExecuteAsync(
                $"CREATE (:TxAtomic {{key: '{keyA}'}}), (:TxAtomic {{key: '{keyB}'}}), (:TxAtomic {{key: '{keyC}'}})");

            using var countResult = await client.ExecuteAsync(
                $"MATCH (n:TxAtomic) WHERE n.key IN ['{keyA}', '{keyB}', '{keyC}'] RETURN count(n) AS total");
            await IntegrationAssertions.AssertSingleIntegerResult(countResult, "total", 3);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task ConstraintViolation_ThrowsAndLeavesFirstCommittedNode(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var key = UniqueValue("txrollback");

            await client.ExecuteAsync(
                "DROP CONSTRAINT txtest_key_unique IF EXISTS");
            await client.ExecuteAsync(
                "CREATE CONSTRAINT txtest_key_unique FOR (n:TxRollback) REQUIRE n.key IS UNIQUE");
            try
            {
                await Assert.That(async () =>
                    await client.ExecuteAsync(
                        $"CREATE (:TxRollback {{key: '{key}'}}), (:TxRollback {{key: '{key}'}})"))
                    .ThrowsException();

                using var countResult = await client.ExecuteAsync(
                    $"MATCH (n:TxRollback {{key: '{key}'}}) RETURN count(n) AS total");
                await IntegrationAssertions.AssertSingleIntegerResult(countResult, "total", 1);
            }
            finally
            {
                await client.ExecuteAsync("DROP CONSTRAINT txtest_key_unique IF EXISTS");
            }
        });
    }
}
