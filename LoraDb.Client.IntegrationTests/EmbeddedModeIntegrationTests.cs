using LoraDb.Client.Native;
using TUnit.Assertions.Extensions;

namespace LoraDb.Client.IntegrationTests;

public class EmbeddedModeIntegrationTests
{
    [Test]
    public async Task ExecuteAsync_EmbeddedMode_WorksWithRealNativeLibrary()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;

        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            throw new InvalidOperationException("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");
        if (!File.Exists(ffiLibraryPath))
            throw new FileNotFoundException($"Native library not found: {ffiLibraryPath}", ffiLibraryPath);

        using var bridge = new PInvokeLoraDbNativeBridge(ffiLibraryPath);
        await using var client = LoraDbClient.CreateEmbedded(bridge);

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
