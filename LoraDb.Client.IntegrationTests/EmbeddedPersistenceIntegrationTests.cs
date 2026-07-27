using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LoraDb.Client.IntegrationTests;

public class EmbeddedPersistenceIntegrationTests
{
    [Test]
    public async Task NamedEmbeddedDatabase_PersistsAcrossReopen()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;

        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        var root = Path.Combine(Path.GetTempPath(), $"loradb-dotnet-integration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var options = new LoraDbEmbeddedOpenOptions
        {
            NativeLibraryName = ffiLibraryPath,
            DatabaseName = "named-persistence",
            DatabaseDirectory = root,
        };

        try
        {
            await using (var writer = LoraDbEmbeddedManagementClient.Create(options))
            {
                using var result = await writer.ExecuteAsync("CREATE (:PersistedUser {id: 1, name: 'Alice'})");
            }

            await using var reader = LoraDbEmbeddedManagementClient.Create(options);
            using var countResult = await reader.ExecuteAsync("MATCH (n:PersistedUser) RETURN count(n) AS total");
            var total = countResult.Root.GetProperty("rows")[0].GetProperty("total").GetInt32();
            await Assert.That(total).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task SnapshotSaveAndLoad_RestoresPreviousState()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;

        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        var root = Path.Combine(Path.GetTempPath(), $"loradb-dotnet-integration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var snapshotPath = Path.Combine(root, "graph.snapshot");
        var options = new LoraDbEmbeddedOpenOptions
        {
            NativeLibraryName = ffiLibraryPath,
            DatabaseName = "snapshot-persistence",
            DatabaseDirectory = root,
        };

        try
        {
            await using var client = LoraDbEmbeddedManagementClient.Create(options);
            using (var _ = await client.ExecuteAsync("CREATE (:SnapUser {id: 1})")) { }
            var saved = await client.SaveSnapshotAsync(snapshotPath);
            await Assert.That(saved.Path).IsEqualTo(snapshotPath);

            using (var _ = await client.ExecuteAsync("CREATE (:SnapUser {id: 2})")) { }
            await client.LoadSnapshotAsync(snapshotPath);

            using var countResult = await client.ExecuteAsync("MATCH (n:SnapUser) RETURN count(n) AS total");
            var total = countResult.Root.GetProperty("rows")[0].GetProperty("total").GetInt32();
            await Assert.That(total).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
