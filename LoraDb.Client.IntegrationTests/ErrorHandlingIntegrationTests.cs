using LoraDb.Client.IntegrationTests.Fixtures;
using LoraDb.Client.IntegrationTests.Helpers;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LoraDb.Client.IntegrationTests;

public class ErrorHandlingIntegrationTests : IntegrationTestBase
{
    [Test]
    [CombinedDataSources]
    public async Task MalformedCypher_ThrowsWithUsefulMessage(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var exception = (await Assert.That(async () => await client.ExecuteAsync("THIS IS NOT CYPHER"))
                .ThrowsException())!;

            await Assert.That(exception.Message).IsNotEmpty();
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task SemanticError_ThrowsWithUsefulMessage(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var exception = (await Assert.That(async () => await client.ExecuteAsync("MATCH (n) RETURN missingVar"))
                .ThrowsException())!;

            await Assert.That(exception.Message).IsNotEmpty();
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task Client_RemainsUsableAfterError(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            await Assert.That(async () => await client.ExecuteAsync("THIS IS NOT CYPHER")).ThrowsException();

            using var result = await client.ExecuteAsync("RETURN 1 AS one");
            await IntegrationAssertions.AssertSingleIntegerResult(result, "one", 1);
        });
    }
}
