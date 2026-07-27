using LoraDb.Client.IntegrationTests.Fixtures;
using LoraDb.Client.IntegrationTests.Helpers;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LoraDb.Client.IntegrationTests;

public class MergeIntegrationTests : IntegrationTestBase
{
    [Test]
    [CombinedDataSources]
    public async Task Merge_CreatesWhenAbsent(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var email = $"{UniqueValue("merge-create")}@example.com";
            using var mergeResult = await client.ExecuteAsync(
                $"MERGE (n:User {{email: '{email}'}}) RETURN n.email AS email");
            await AssertSingleStringResult(mergeResult, "email", email);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task Merge_IsIdempotent(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var email = $"{UniqueValue("merge-idem")}@example.com";
            using var firstResult = await client.ExecuteAsync($"MERGE (n:User {{email: '{email}'}}) RETURN n");
            using var secondResult = await client.ExecuteAsync($"MERGE (n:User {{email: '{email}'}}) RETURN n");
            using var countResult = await client.ExecuteAsync(
                $"MATCH (n:User {{email: '{email}'}}) RETURN count(n) AS total");
            await IntegrationAssertions.AssertSingleIntegerResult(countResult, "total", 1);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task MergeWithOnCreateSet_AppliesOnlyOnCreation(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var email = $"{UniqueValue("merge-oncreate")}@example.com";
            using var firstResult = await client.ExecuteAsync(
                $"MERGE (n:User {{email: '{email}'}}) ON CREATE SET n.createdAt = 1 RETURN n.createdAt AS createdAt");
            using var secondResult = await client.ExecuteAsync(
                $"MERGE (n:User {{email: '{email}'}}) ON CREATE SET n.createdAt = 2 RETURN n.createdAt AS createdAt");
            await Assert.That(IntegrationAssertions.GetRowColumn(secondResult, 0, "createdAt").GetInt32()).IsEqualTo(1);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task MergeWithOnMatchSet_AppliesOnlyOnMatch(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var email = $"{UniqueValue("merge-onmatch")}@example.com";
            using var createResult = await client.ExecuteAsync(
                $"MERGE (n:User {{email: '{email}'}}) ON CREATE SET n.updatedAt = 0 RETURN n.email AS email");
            using var matchResult = await client.ExecuteAsync(
                $"MERGE (n:User {{email: '{email}'}}) ON MATCH SET n.updatedAt = 2 RETURN n.updatedAt AS updatedAt");
            await Assert.That(IntegrationAssertions.GetRowColumn(matchResult, 0, "updatedAt").GetInt32()).IsEqualTo(2);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task MergeRelationship_BetweenExistingNodes_IsCreatedOnce(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var firstResult = await client.ExecuteAsync(
                "MATCH (a:Person {name: 'Carol'}), (b:Person {name: 'Dave'}) MERGE (a)-[:KNOWS]->(b) RETURN a, b");
            using var secondResult = await client.ExecuteAsync(
                "MATCH (a:Person {name: 'Carol'}), (b:Person {name: 'Dave'}) MERGE (a)-[:KNOWS]->(b) RETURN a, b");
            using var countResult = await client.ExecuteAsync(
                "MATCH (:Person {name: 'Carol'})-[r:KNOWS]->(:Person {name: 'Dave'}) RETURN count(r) AS total");
            await IntegrationAssertions.AssertSingleIntegerResult(countResult, "total", 1);
        });
    }
}
