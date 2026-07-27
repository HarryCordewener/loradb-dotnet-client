using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LoraDb.Client.IntegrationTests;

/// <summary>
/// Integration tests for WAL-backed embedded persistence using
/// <see cref="LoraDbEmbeddedOpenOptions.WalDirectory"/>.
/// Verifies that data written to a WAL database survives close and reopen across
/// a fresh <see cref="LoraDbEmbeddedManagementClient"/> instance.
/// Note: <see cref="LoraDbEmbeddedOpenOptions.WalDirectory"/> and
/// <see cref="LoraDbEmbeddedOpenOptions.DatabaseName"/> are mutually exclusive.
/// </summary>
public class EmbeddedWalPersistenceIntegrationTests
{
    [Test]
    public async Task WalDatabase_SingleWrite_PersistsAcrossReopen()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;
        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        var walDir = Path.Combine(Path.GetTempPath(), $"loradb-wal-single-{Guid.NewGuid():N}");
        Directory.CreateDirectory(walDir);
        var key = $"wal-single-{Guid.NewGuid():N}";
        var options = new LoraDbEmbeddedOpenOptions
        {
            NativeLibraryName = ffiLibraryPath,
            WalDirectory = walDir,
        };

        try
        {
            await using (var writer = LoraDbEmbeddedManagementClient.Create(options))
            {
                using var _ = await writer.ExecuteAsync(
                    $"CREATE (:WalSingle {{key: '{key}', value: 1}})");
            }

            await using var reader = LoraDbEmbeddedManagementClient.Create(options);
            using var countResult = await reader.ExecuteAsync(
                $"MATCH (n:WalSingle {{key: '{key}'}}) RETURN count(n) AS total");
            var total = countResult.Root.GetProperty("rows")[0].GetProperty("total").GetInt32();
            await Assert.That(total).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(walDir, recursive: true);
        }
    }

    [Test]
    public async Task WalDatabase_MultipleWrites_AllPersistAcrossReopen()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;
        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        const int nodeCount = 5;
        var walDir = Path.Combine(Path.GetTempPath(), $"loradb-wal-multi-{Guid.NewGuid():N}");
        Directory.CreateDirectory(walDir);
        var tag = $"wal-multi-{Guid.NewGuid():N}";
        var options = new LoraDbEmbeddedOpenOptions
        {
            NativeLibraryName = ffiLibraryPath,
            WalDirectory = walDir,
        };

        try
        {
            await using (var writer = LoraDbEmbeddedManagementClient.Create(options))
            {
                for (var i = 0; i < nodeCount; i++)
                {
                    using var _ = await writer.ExecuteAsync(
                        $"CREATE (:WalMulti {{tag: '{tag}', idx: {i}, key: '{Guid.NewGuid():N}'}})");
                }
            }

            await using var reader = LoraDbEmbeddedManagementClient.Create(options);
            using var countResult = await reader.ExecuteAsync(
                $"MATCH (n:WalMulti {{tag: '{tag}'}}) RETURN count(n) AS total");
            var total = countResult.Root.GetProperty("rows")[0].GetProperty("total").GetInt32();
            await Assert.That(total).IsEqualTo(nodeCount);
        }
        finally
        {
            Directory.Delete(walDir, recursive: true);
        }
    }

    [Test]
    public async Task WalDatabase_ExactPropertyValue_RoundTripsCorrectly()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;
        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        var walDir = Path.Combine(Path.GetTempPath(), $"loradb-wal-value-{Guid.NewGuid():N}");
        Directory.CreateDirectory(walDir);
        var key = $"wal-value-{Guid.NewGuid():N}";
        var expectedName = $"name-{Guid.NewGuid():N}";
        var options = new LoraDbEmbeddedOpenOptions
        {
            NativeLibraryName = ffiLibraryPath,
            WalDirectory = walDir,
        };

        try
        {
            await using (var writer = LoraDbEmbeddedManagementClient.Create(options))
            {
                using var _ = await writer.ExecuteAsync(
                    $"CREATE (:WalValue {{key: '{key}', name: '{expectedName}', score: 99}})");
            }

            await using var reader = LoraDbEmbeddedManagementClient.Create(options);
            using var result = await reader.ExecuteAsync(
                $"MATCH (n:WalValue {{key: '{key}'}}) RETURN n.name AS name, n.score AS score");
            var row = result.Root.GetProperty("rows")[0];
            await Assert.That(row.GetProperty("name").GetString()).IsEqualTo(expectedName);
            await Assert.That(row.GetProperty("score").GetInt32()).IsEqualTo(99);
        }
        finally
        {
            Directory.Delete(walDir, recursive: true);
        }
    }

    [Test]
    public async Task WalDatabase_DeleteAfterWrite_DeletePersistsAcrossReopen()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;
        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        var walDir = Path.Combine(Path.GetTempPath(), $"loradb-wal-delete-{Guid.NewGuid():N}");
        Directory.CreateDirectory(walDir);
        var keepKey = $"wal-keep-{Guid.NewGuid():N}";
        var deleteKey = $"wal-delete-{Guid.NewGuid():N}";
        var options = new LoraDbEmbeddedOpenOptions
        {
            NativeLibraryName = ffiLibraryPath,
            WalDirectory = walDir,
        };

        try
        {
            await using (var writer = LoraDbEmbeddedManagementClient.Create(options))
            {
                using var _ = await writer.ExecuteAsync(
                    $"CREATE (:WalDeleteTest {{key: '{keepKey}'}}), (:WalDeleteTest {{key: '{deleteKey}'}})");
                using var __ = await writer.ExecuteAsync(
                    $"MATCH (n:WalDeleteTest {{key: '{deleteKey}'}}) DETACH DELETE n");
            }

            await using var reader = LoraDbEmbeddedManagementClient.Create(options);
            using var countAll = await reader.ExecuteAsync(
                "MATCH (n:WalDeleteTest) RETURN count(n) AS total");
            var total = countAll.Root.GetProperty("rows")[0].GetProperty("total").GetInt32();
            await Assert.That(total).IsEqualTo(1);

            using var countDeleted = await reader.ExecuteAsync(
                $"MATCH (n:WalDeleteTest {{key: '{deleteKey}'}}) RETURN count(n) AS total");
            var deletedCount = countDeleted.Root.GetProperty("rows")[0].GetProperty("total").GetInt32();
            await Assert.That(deletedCount).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(walDir, recursive: true);
        }
    }

    [Test]
    public async Task WalDatabase_RelationshipBetweenNodes_PersistsAcrossReopen()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;
        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        var walDir = Path.Combine(Path.GetTempPath(), $"loradb-wal-rel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(walDir);
        var aKey = $"wal-a-{Guid.NewGuid():N}";
        var bKey = $"wal-b-{Guid.NewGuid():N}";
        var options = new LoraDbEmbeddedOpenOptions
        {
            NativeLibraryName = ffiLibraryPath,
            WalDirectory = walDir,
        };

        try
        {
            await using (var writer = LoraDbEmbeddedManagementClient.Create(options))
            {
                using var _ = await writer.ExecuteAsync(
                    $"CREATE (:WalNode {{key: '{aKey}'}})-[:WAL_LINKS {{weight: 5}}]->(:WalNode {{key: '{bKey}'}})");
            }

            await using var reader = LoraDbEmbeddedManagementClient.Create(options);
            using var result = await reader.ExecuteAsync(
                $"MATCH (:WalNode {{key: '{aKey}'}})-[r:WAL_LINKS]->(:WalNode {{key: '{bKey}'}}) RETURN r.weight AS weight");
            var weight = result.Root.GetProperty("rows")[0].GetProperty("weight").GetInt32();
            await Assert.That(weight).IsEqualTo(5);
        }
        finally
        {
            Directory.Delete(walDir, recursive: true);
        }
    }

    [Test]
    public async Task WalDatabase_SnapshotSaveAndLoad_RestoresWalState()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;
        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        var walDir = Path.Combine(Path.GetTempPath(), $"loradb-wal-snap-{Guid.NewGuid():N}");
        Directory.CreateDirectory(walDir);
        var snapshotPath = Path.Combine(walDir, "graph.snapshot");
        var key = $"wal-snap-{Guid.NewGuid():N}";
        var options = new LoraDbEmbeddedOpenOptions
        {
            NativeLibraryName = ffiLibraryPath,
            WalDirectory = walDir,
        };

        try
        {
            await using var client = LoraDbEmbeddedManagementClient.Create(options);
            using (var _ = await client.ExecuteAsync($"CREATE (:WalSnap {{key: '{key}'}})")) { }

            var saved = await client.SaveSnapshotAsync(snapshotPath);
            await Assert.That(saved.Path).IsEqualTo(snapshotPath);
            await Assert.That(saved.WalLsn).IsNull();

            // Write an additional node, then restore — it should disappear.
            using (var __ = await client.ExecuteAsync($"CREATE (:WalSnap {{key: 'extra-{Guid.NewGuid():N}'}})")) { }
            await client.LoadSnapshotAsync(snapshotPath);

            using var countResult = await client.ExecuteAsync("MATCH (n:WalSnap) RETURN count(n) AS total");
            var total = countResult.Root.GetProperty("rows")[0].GetProperty("total").GetInt32();
            await Assert.That(total).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(walDir, recursive: true);
        }
    }
}
