namespace LoraDb.Client.IntegrationTests;

internal static class IntegrationTestEnvironment
{
    private const string RunFlagName = "LORADB_RUN_INTEGRATION_TESTS";
    private const string HttpImageName = "LORADB_HTTP_IMAGE";
    private const string FfiLibraryPathName = "LORADB_FFI_LIBRARY_PATH";

    public static bool IsEnabled()
    {
        var value = Environment.GetEnvironmentVariable(RunFlagName);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private const string DefaultHttpImage = "ghcr.io/lora-db/lora-server:latest";

    public static string HttpImage =>
        Environment.GetEnvironmentVariable(HttpImageName) ?? DefaultHttpImage;

    public static string? FfiLibraryPath => Environment.GetEnvironmentVariable(FfiLibraryPathName);
}
