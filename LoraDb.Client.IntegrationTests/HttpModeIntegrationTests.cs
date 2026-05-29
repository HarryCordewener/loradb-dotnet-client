using DotNet.Testcontainers.Builders;
using TUnit.Assertions.Extensions;

namespace LoraDb.Client.IntegrationTests;

public class HttpModeIntegrationTests
{
    [Test]
    public async Task ExecuteAsync_HttpMode_WorksAgainstRealServerContainer()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;

        var image = IntegrationTestEnvironment.HttpImage;
        if (string.IsNullOrWhiteSpace(image))
            throw new InvalidOperationException("Set LORADB_HTTP_IMAGE when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        await using var container = new ContainerBuilder(image)
            .WithPortBinding(4747, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(4747))
            .Build();

        await container.StartAsync();

        var port = container.GetMappedPublicPort(4747);
        var endpoint = new Uri($"http://127.0.0.1:{port}/");

        await using var client = LoraDbClient.CreateHttp(endpoint);
        using var result = await client.ExecuteAsync("RETURN 1 AS one");
        await AssertSingleIntegerResult(result, "one", 1);

        using var parameterizedResult = await client.ExecuteAsync(
            "RETURN $value + 1 AS incremented",
            new Dictionary<string, object?> { ["value"] = 1 });
        await AssertSingleIntegerResult(parameterizedResult, "incremented", 2);
    }

    private static async Task AssertSingleIntegerResult(LoraDbQueryResult result, string column, int expected)
    {
        var rows = result.Root.GetProperty("rows");
        await Assert.That(rows.GetArrayLength()).IsEqualTo(1);

        var value = rows[0].GetProperty(column).GetInt32();
        await Assert.That(value).IsEqualTo(expected);
    }
}
