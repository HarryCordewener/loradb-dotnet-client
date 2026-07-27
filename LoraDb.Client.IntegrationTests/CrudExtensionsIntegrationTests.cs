using System.Text.Json.Serialization;
using LoraDb.Client.IntegrationTests.Fixtures;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LoraDb.Client.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="LoraDbClientCrudExtensions"/>.
/// These tests run against a real LoraDB instance and verify that the generated
/// Cypher queries produce the expected results.
/// </summary>
public class CrudExtensionsIntegrationTests : IntegrationTestBase
{
    // ── CreateNodeAsync ────────────────────────────────────────────────────────

    [Test]
    [CombinedDataSources]
    public async Task CreateNode_WithProperties_CanBeMatchedBack(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var key = UniqueValue("crud-create");

            var row = await client.CreateNodeAsync<NodeRow>("CrudPerson",
                new Dictionary<string, object?> { ["key"] = key, ["name"] = "Alice" });

            await Assert.That(row.N.Labels).Contains("CrudPerson");
            await Assert.That(row.N.Properties.GetProperty("key").GetString()).IsEqualTo(key);
            await Assert.That(row.N.Properties.GetProperty("name").GetString()).IsEqualTo("Alice");

            // Verify the node is visible via a raw query
            using var matchResult = await client.ExecuteAsync(
                $"MATCH (n:CrudPerson {{key: '{key}'}}) RETURN count(n) AS total");
            await Helpers.IntegrationAssertions.AssertSingleIntegerResult(matchResult, "total", 1);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task CreateNode_WithoutProperties_CanBeMatchedBack(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var row = await client.CreateNodeAsync<NodeRow>("CrudEmpty");

            await Assert.That(row.N.Labels).Contains("CrudEmpty");

            using var countResult = await client.ExecuteAsync("MATCH (n:CrudEmpty) RETURN count(n) AS total");
            await Helpers.IntegrationAssertions.AssertSingleIntegerResult(countResult, "total", 1);
        });
    }

    // ── FindNodesAsync ─────────────────────────────────────────────────────────

    [Test]
    [CombinedDataSources]
    public async Task FindNodes_WithFilter_ReturnsMatchingNodes(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var key = UniqueValue("crud-find");
            using var _ = await client.ExecuteAsync(
                $"CREATE (:FindTest {{key: '{key}', active: true}}), (:FindTest {{key: '{key}-other', active: false}})");

            var rows = new List<NodeRow>();
            await foreach (var row in client.FindNodesAsync<NodeRow>("FindTest",
                               new Dictionary<string, object?> { ["key"] = key }))
                rows.Add(row);

            await Assert.That(rows.Count).IsEqualTo(1);
            await Assert.That(rows[0].N.Properties.GetProperty("key").GetString()).IsEqualTo(key);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task FindNodes_WithMultipleFilters_ReturnsPreciseMatch(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var tag = UniqueValue("crud-find-multi");
            using var _ = await client.ExecuteAsync(
                $"CREATE (:MultiFilter {{tag: '{tag}', name: 'Alice', active: true}}), " +
                $"       (:MultiFilter {{tag: '{tag}', name: 'Alice', active: false}}), " +
                $"       (:MultiFilter {{tag: '{tag}', name: 'Bob', active: true}})");

            var rows = new List<NodeRow>();
            await foreach (var row in client.FindNodesAsync<NodeRow>("MultiFilter",
                               new Dictionary<string, object?> { ["tag"] = tag, ["name"] = "Alice" }))
                rows.Add(row);

            await Assert.That(rows.Count).IsEqualTo(2);
            foreach (var row in rows)
                await Assert.That(row.N.Properties.GetProperty("name").GetString()).IsEqualTo("Alice");
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task FindNodes_WithoutFilter_ReturnsAllNodesWithLabel(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var tag = UniqueValue("crud-find-all");
            using var _ = await client.ExecuteAsync(
                $"CREATE (:AllNodes {{tag: '{tag}', n: 1}}), (:AllNodes {{tag: '{tag}', n: 2}}), (:AllNodes {{tag: '{tag}', n: 3}})");

            var rows = new List<NodeRow>();
            await foreach (var row in client.FindNodesAsync<NodeRow>("AllNodes",
                               new Dictionary<string, object?> { ["tag"] = tag }))
                rows.Add(row);

            await Assert.That(rows.Count).IsEqualTo(3);
        });
    }

    // ── FindNodeAsync ──────────────────────────────────────────────────────────

    [Test]
    [CombinedDataSources]
    public async Task FindNode_WithFilter_ReturnsFirstMatch(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var key = UniqueValue("crud-findone");
            using var _ = await client.ExecuteAsync($"CREATE (:FindOne {{key: '{key}', name: 'Target'}})");

            var row = await client.FindNodeAsync<NodeRow>("FindOne",
                new Dictionary<string, object?> { ["key"] = key });

            await Assert.That(row).IsNotNull();
            await Assert.That(row!.N.Properties.GetProperty("name").GetString()).IsEqualTo("Target");
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task FindNode_NoMatch_ReturnsNull(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var row = await client.FindNodeAsync<NodeRow>("NonExistentLabel9999");

            await Assert.That(row).IsNull();
        });
    }

    // ── UpdateNodesAsync ───────────────────────────────────────────────────────

    [Test]
    [CombinedDataSources]
    public async Task UpdateNodes_ChangesPropertyValue(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var key = UniqueValue("crud-update");
            using var _ = await client.ExecuteAsync($"CREATE (:UpdateTest {{key: '{key}', score: 10}})");

            var updated = new List<NodeRow>();
            await foreach (var row in client.UpdateNodesAsync<NodeRow>("UpdateTest",
                               match: new Dictionary<string, object?> { ["key"] = key },
                               properties: new Dictionary<string, object?> { ["score"] = 99 }))
                updated.Add(row);

            await Assert.That(updated.Count).IsEqualTo(1);
            await Assert.That(updated[0].N.Properties.GetProperty("score").GetInt32()).IsEqualTo(99);

            // Verify via raw query
            using var verify = await client.ExecuteAsync(
                $"MATCH (n:UpdateTest {{key: '{key}'}}) RETURN n.score AS score");
            await Helpers.IntegrationAssertions.AssertSingleIntegerResult(verify, "score", 99);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task UpdateNodes_MultipleProperties_AllPropertiesUpdated(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var key = UniqueValue("crud-update-multi");
            using var _ = await client.ExecuteAsync(
                $"CREATE (:MultiUpdate {{key: '{key}', score: 1, label: 'old', active: false}})");

            var updated = new List<NodeRow>();
            await foreach (var row in client.UpdateNodesAsync<NodeRow>("MultiUpdate",
                               match: new Dictionary<string, object?> { ["key"] = key },
                               properties: new Dictionary<string, object?> { ["score"] = 99, ["label"] = "new", ["active"] = true }))
                updated.Add(row);

            await Assert.That(updated.Count).IsEqualTo(1);
            await Assert.That(updated[0].N.Properties.GetProperty("score").GetInt32()).IsEqualTo(99);
            await Assert.That(updated[0].N.Properties.GetProperty("label").GetString()).IsEqualTo("new");
            await Assert.That(updated[0].N.Properties.GetProperty("active").GetBoolean()).IsTrue();
        });
    }

    // ── DeleteNodesAsync ───────────────────────────────────────────────────────

    [Test]
    [CombinedDataSources]
    public async Task DeleteNodes_WithMatch_RemovesMatchingNodes(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var key = UniqueValue("crud-delete");
            using var _ = await client.ExecuteAsync($"CREATE (:DeleteTest {{key: '{key}'}}), (:DeleteTest {{key: 'keep'}})");

            await client.DeleteNodesAsync("DeleteTest",
                match: new Dictionary<string, object?> { ["key"] = key });

            using var countResult = await client.ExecuteAsync(
                "MATCH (n:DeleteTest) RETURN count(n) AS total");
            await Helpers.IntegrationAssertions.AssertSingleIntegerResult(countResult, "total", 1);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task DeleteNodes_WithoutMatch_RemovesAllNodesWithLabel(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            using var _ = await client.ExecuteAsync("CREATE (:ClearAll {n:1}), (:ClearAll {n:2})");

            await client.DeleteNodesAsync("ClearAll");

            using var countResult = await client.ExecuteAsync("MATCH (n:ClearAll) RETURN count(n) AS total");
            await Helpers.IntegrationAssertions.AssertSingleIntegerResult(countResult, "total", 0);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task DeleteNodes_WithDetachFalse_RemovesIsolatedNodes(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var key = UniqueValue("crud-nodetach");
            using var _ = await client.ExecuteAsync($"CREATE (:NoDetachNode {{key: '{key}'}})");

            await client.DeleteNodesAsync("NoDetachNode",
                match: new Dictionary<string, object?> { ["key"] = key },
                detach: false);

            using var countResult = await client.ExecuteAsync(
                $"MATCH (n:NoDetachNode {{key: '{key}'}}) RETURN count(n) AS total");
            await Helpers.IntegrationAssertions.AssertSingleIntegerResult(countResult, "total", 0);
        });
    }

    // ── MergeNodeAsync ─────────────────────────────────────────────────────────

    [Test]
    [CombinedDataSources]
    public async Task MergeNode_CreatesNodeWhenAbsent(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var email = $"crud-merge-{Guid.NewGuid():N}@example.com";

            var row = await client.MergeNodeAsync<NodeRow>("MergeUser",
                new Dictionary<string, object?> { ["email"] = email });

            await Assert.That(row.N.Properties.GetProperty("email").GetString()).IsEqualTo(email);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task MergeNode_DoesNotDuplicateExistingNode(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var email = $"crud-merge-dedup-{Guid.NewGuid():N}@example.com";

            // Create once via raw query
            using var _ = await client.ExecuteAsync($"CREATE (:MergeDedup {{email: '{email}'}})");

            // Merge should match the existing node, not create a second one
            var merged = await client.MergeNodeAsync<NodeRow>("MergeDedup",
                new Dictionary<string, object?> { ["email"] = email });

            using var countResult = await client.ExecuteAsync(
                $"MATCH (n:MergeDedup {{email: '{email}'}}) RETURN count(n) AS total");
            await Helpers.IntegrationAssertions.AssertSingleIntegerResult(countResult, "total", 1);
        });
    }

    // ── DTOs ───────────────────────────────────────────────────────────────────

    public sealed class NodeRow
    {
        [JsonPropertyName("n")]
        public NodeData N { get; init; } = null!;
    }

    public sealed class NodeData
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("labels")]
        public List<string> Labels { get; init; } = new();

        [JsonPropertyName("properties")]
        public System.Text.Json.JsonElement Properties { get; init; }
    }
}
