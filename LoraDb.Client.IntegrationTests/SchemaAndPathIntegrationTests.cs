using LoraDb.Client.IntegrationTests.Fixtures;
using LoraDb.Client.IntegrationTests.Helpers;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LoraDb.Client.IntegrationTests;

public class SchemaAndPathIntegrationTests : IntegrationTestBase
{
    [Test]
    [CombinedDataSources]
    public async Task IndexCatalog_CreateShowDrop_WorksAcrossModes(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var indexName = UniqueValue("idx_person_name");
            try
            {
                await client.ExecuteAsync($"CREATE INDEX {indexName} FOR (p:Person) ON (p.name)");

                using var showAfterCreate = await client.ExecuteAsync(
                    $"SHOW INDEXES YIELD name WHERE name = '{indexName}' RETURN count(name) AS total");
                await IntegrationAssertions.AssertSingleIntegerResult(showAfterCreate, "total", 1);
            }
            finally
            {
                await client.ExecuteAsync($"DROP INDEX {indexName} IF EXISTS");
            }

            using var showAfterDrop = await client.ExecuteAsync(
                $"SHOW INDEXES YIELD name WHERE name = '{indexName}' RETURN count(name) AS total");
            await IntegrationAssertions.AssertSingleIntegerResult(showAfterDrop, "total", 0);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task ConstraintCatalog_CreateAndEnforceExistence_WorksAcrossModes(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var constraintName = UniqueValue("constraint_person_name");
            try
            {
                await client.ExecuteAsync(
                    $"CREATE CONSTRAINT {constraintName} FOR (p:Person) REQUIRE p.name IS NOT NULL");

                await client.ExecuteAsync("CREATE (:Person {name: 'Alice'})");

                await Assert.That(async () =>
                        await client.ExecuteAsync("MATCH (p:Person {name: 'Alice'}) REMOVE p.name"))
                    .ThrowsException();

                using var show = await client.ExecuteAsync(
                    $"SHOW CONSTRAINTS YIELD name WHERE name = '{constraintName}' RETURN count(name) AS total");
                await IntegrationAssertions.AssertSingleIntegerResult(show, "total", 1);
            }
            finally
            {
                await client.ExecuteAsync($"DROP CONSTRAINT {constraintName} IF EXISTS");
            }
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task Paths_VariableLengthTraversal_ReturnsExpectedHopCount(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH p = (:Person {name: 'Alice'})-[*2..2]->(:Person {name: 'Carol'}) RETURN path.length(p) AS hops");
            await IntegrationAssertions.AssertSingleIntegerResult(result, "hops", 2);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task Paths_ShortestPath_ReturnsExpectedHopCount(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH p = shortestPath((:Person {name: 'Alice'})-[*]->(:Person {name: 'Carol'})) RETURN path.length(p) AS hops");
            await IntegrationAssertions.AssertSingleIntegerResult(result, "hops", 2);
        });
    }
}
