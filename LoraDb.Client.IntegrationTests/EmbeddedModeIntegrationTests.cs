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
        await Assert.That(result.Root.TryGetProperty("rows", out _)).IsTrue();
    }
}
