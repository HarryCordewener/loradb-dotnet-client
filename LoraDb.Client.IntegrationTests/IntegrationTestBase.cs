using System.Text.Json;
using LoraDb.Client.IntegrationTests.Fixtures;
using LoraDb.Client.IntegrationTests.Seeds;
using TUnit.Core;

namespace LoraDb.Client.IntegrationTests;

public abstract class IntegrationTestBase
{
    private static readonly SemaphoreSlim DatabaseLock = new(1, 1);

    protected static async Task WithCleanDatabaseAsync(ILoraDbClientFixture fixture, Func<LoraDbClient, Task> test)
    {
        await WithSeedAsync(fixture, null, test);
    }

    protected static async Task WithSocialGraphAsync(ILoraDbClientFixture fixture, Func<LoraDbClient, Task> test)
    {
        await WithSeedAsync(fixture, SeedGraph.CreateSocialGraph, test);
    }

    protected static async Task WithProductGraphAsync(ILoraDbClientFixture fixture, Func<LoraDbClient, Task> test)
    {
        await WithSeedAsync(fixture, SeedGraph.CreateProductGraph, test);
    }

    protected static async Task WithGraphsAsync(ILoraDbClientFixture fixture, Func<LoraDbClient, Task> test)
    {
        await WithSeedAsync(
            fixture,
            async client =>
            {
                await SeedGraph.CreateSocialGraph(client);
                await SeedGraph.CreateProductGraph(client);
            },
            test);
    }

    protected static string UniqueValue(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    protected static bool IsHttpFixture(ILoraDbClientFixture fixture) => fixture is HttpClientFixture;

    protected static async Task AssertSingleBooleanResult(LoraDbQueryResult result, string column, bool expected)
    {
        var actual = Helpers.IntegrationAssertions.GetRowColumn(result, 0, column).GetBoolean();
        await Assert.That(actual).IsEqualTo(expected);
    }

    protected static async Task AssertSingleStringResult(LoraDbQueryResult result, string column, string expected)
    {
        var actual = Helpers.IntegrationAssertions.GetRowColumn(result, 0, column).GetString();
        await Assert.That(actual).IsEqualTo(expected);
    }

    protected static async Task AssertStringRowsAsync(LoraDbQueryResult result, string column, params string[] expected)
    {
        await Helpers.IntegrationAssertions.AssertRowCount(result, expected.Length);

        var rows = result.Root.GetProperty("rows");
        for (var index = 0; index < expected.Length; index++)
        {
            await Assert.That(rows[index].GetProperty(column).GetString()).IsEqualTo(expected[index]);
        }
    }

    protected static async Task AssertRowArrayStringsAsync(JsonElement root, params string[] expected)
    {
        var rowArrays = root.GetProperty("rowArrays");
        await Assert.That(rowArrays.GetArrayLength()).IsEqualTo(expected.Length);

        for (var index = 0; index < expected.Length; index++)
        {
            await Assert.That(rowArrays[index][0].GetString()).IsEqualTo(expected[index]);
        }
    }

    private static async Task WithSeedAsync(
        ILoraDbClientFixture fixture,
        Func<LoraDbClient, Task>? seed,
        Func<LoraDbClient, Task> test)
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;

        await DatabaseLock.WaitAsync();
        try
        {
            await ResetAsync(fixture);

            if (seed is not null)
            {
                await using var seedClient = fixture.CreateClient();
                await seed(seedClient);
            }

            await using var client = fixture.CreateClient();
            await test(client);
        }
        finally
        {
            try
            {
                await ResetAsync(fixture);
            }
            finally
            {
                DatabaseLock.Release();
            }
        }
    }

    private static async Task ResetAsync(ILoraDbClientFixture fixture)
    {
        await using var client = fixture.CreateClient();
        await SeedGraph.TearDownGraph(client);
    }
}
