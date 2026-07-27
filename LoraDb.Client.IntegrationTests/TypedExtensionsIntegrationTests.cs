using System.Text.Json;
using System.Text.Json.Serialization;
using LoraDb.Client.IntegrationTests.Fixtures;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LoraDb.Client.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="LoraDbClientTypedExtensions"/>.
/// Verifies that <c>ExecuteRowsAsync</c> streams typed results correctly against a real LoraDB
/// instance using both reflection-based and source-generated deserialization.
/// </summary>
public class TypedExtensionsIntegrationTests : IntegrationTestBase
{
    // ── ExecuteRowsAsync<T> reflection-based ───────────────────────────────────

    [Test]
    [CombinedDataSources]
    public async Task ExecuteRowsAsync_Generic_StreamsTypedRows(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var tag = UniqueValue("typed-stream");
            using var _ = await client.ExecuteAsync(
                $"CREATE (:TypedPerson {{tag: '{tag}', name: 'Alice'}}), (:TypedPerson {{tag: '{tag}', name: 'Bob'}})");

            var rows = new List<TypedNameRow>();
            await foreach (var row in client.ExecuteRowsAsync<TypedNameRow>(
                               $"MATCH (n:TypedPerson {{tag: '{tag}'}}) RETURN n.name AS name ORDER BY n.name"))
                rows.Add(row);

            await Assert.That(rows.Count).IsEqualTo(2);
            await Assert.That(rows[0].Name).IsEqualTo("Alice");
            await Assert.That(rows[1].Name).IsEqualTo("Bob");
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task ExecuteRowsAsync_Generic_WithParameters_StreamsTypedRows(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var tag = UniqueValue("typed-param");
            using var _ = await client.ExecuteAsync(
                $"CREATE (:TaggedPerson {{tag: '{tag}', name: 'Carol'}}), (:TaggedPerson {{tag: '{tag}', name: 'Dave'}})");

            var rows = new List<TypedNameRow>();
            await foreach (var row in client.ExecuteRowsAsync<TypedNameRow>(
                               "MATCH (n:TaggedPerson {tag: $tag}) RETURN n.name AS name ORDER BY n.name",
                               parameters: new Dictionary<string, object?> { ["tag"] = tag }))
                rows.Add(row);

            await Assert.That(rows.Count).IsEqualTo(2);
            await Assert.That(rows[0].Name).IsEqualTo("Carol");
            await Assert.That(rows[1].Name).IsEqualTo("Dave");
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task ExecuteRowsAsync_Generic_EmptyResult_YieldsNoRows(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var tag = UniqueValue("typed-empty");

            var rows = new List<TypedNameRow>();
            await foreach (var row in client.ExecuteRowsAsync<TypedNameRow>(
                               $"MATCH (n:TypedEmpty {{tag: '{tag}'}}) RETURN n.name AS name"))
                rows.Add(row);

            await Assert.That(rows.Count).IsEqualTo(0);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task ExecuteRowsAsync_Generic_SingleRow_YieldsExactlyOneRow(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var key = UniqueValue("typed-single");
            var name = UniqueValue("name");
            using var _ = await client.ExecuteAsync($"CREATE (:SingleTyped {{key: '{key}', name: '{name}'}})");

            var rows = new List<TypedNameRow>();
            await foreach (var row in client.ExecuteRowsAsync<TypedNameRow>(
                               $"MATCH (n:SingleTyped {{key: '{key}'}}) RETURN n.name AS name"))
                rows.Add(row);

            await Assert.That(rows.Count).IsEqualTo(1);
            await Assert.That(rows[0].Name).IsEqualTo(name);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task ExecuteRowsAsync_Generic_MultipleNodes_CorrectlyStreamsAll(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var tag = UniqueValue("typed-multi");
            var names = new[] { UniqueValue("n"), UniqueValue("n"), UniqueValue("n"), UniqueValue("n"), UniqueValue("n") };

            var creates = string.Join(", ", names.Select(n => $"(:MultiStream {{tag: '{tag}', name: '{n}'}})"));
            using var _ = await client.ExecuteAsync($"CREATE {creates}");

            var rows = new List<TypedNameRow>();
            await foreach (var row in client.ExecuteRowsAsync<TypedNameRow>(
                               $"MATCH (n:MultiStream {{tag: '{tag}'}}) RETURN n.name AS name ORDER BY n.name"))
                rows.Add(row);

            await Assert.That(rows.Count).IsEqualTo(names.Length);
        });
    }

    // ── ExecuteRowsAsync<T> source-generated (JsonTypeInfo) ───────────────────

    [Test]
    [CombinedDataSources]
    public async Task ExecuteRowsAsync_WithTypeInfo_StreamsTypedRows(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var tag = UniqueValue("typed-typeinfo");
            using var _ = await client.ExecuteAsync(
                $"CREATE (:TypeInfoPerson {{tag: '{tag}', name: 'Eve'}}), (:TypeInfoPerson {{tag: '{tag}', name: 'Frank'}})");

            var rows = new List<TypedNameRow>();
            await foreach (var row in client.ExecuteRowsAsync(
                               $"MATCH (n:TypeInfoPerson {{tag: '{tag}'}}) RETURN n.name AS name ORDER BY n.name",
                               TypedExtensionsJsonContext.Default.TypedNameRow))
                rows.Add(row);

            await Assert.That(rows.Count).IsEqualTo(2);
            await Assert.That(rows[0].Name).IsEqualTo("Eve");
            await Assert.That(rows[1].Name).IsEqualTo("Frank");
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task ExecuteRowsAsync_WithTypeInfo_WithParameters_StreamsTypedRows(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var tag = UniqueValue("typed-typeinfo-param");
            using var _ = await client.ExecuteAsync(
                $"CREATE (:TiParamPerson {{tag: '{tag}', name: 'Grace'}}), (:TiParamPerson {{tag: '{tag}', name: 'Hank'}})");

            var rows = new List<TypedNameRow>();
            await foreach (var row in client.ExecuteRowsAsync(
                               "MATCH (n:TiParamPerson {tag: $tag}) RETURN n.name AS name ORDER BY n.name",
                               TypedExtensionsJsonContext.Default.TypedNameRow,
                               parameters: new Dictionary<string, object?> { ["tag"] = tag }))
                rows.Add(row);

            await Assert.That(rows.Count).IsEqualTo(2);
            await Assert.That(rows[0].Name).IsEqualTo("Grace");
            await Assert.That(rows[1].Name).IsEqualTo("Hank");
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task ExecuteRowsAsync_WithTypeInfo_EmptyResult_YieldsNoRows(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var tag = UniqueValue("typed-typeinfo-empty");

            var rows = new List<TypedNameRow>();
            await foreach (var row in client.ExecuteRowsAsync(
                               $"MATCH (n:TiEmpty {{tag: '{tag}'}}) RETURN n.name AS name",
                               TypedExtensionsJsonContext.Default.TypedNameRow))
                rows.Add(row);

            await Assert.That(rows.Count).IsEqualTo(0);
        });
    }

    // ── Mixed-type results ─────────────────────────────────────────────────────

    [Test]
    [CombinedDataSources]
    public async Task ExecuteRowsAsync_TypedIntegerRow_StreamsNumericValues(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var tag = UniqueValue("typed-int");
            using var _ = await client.ExecuteAsync(
                $"CREATE (:IntNode {{tag: '{tag}', score: 10}}), (:IntNode {{tag: '{tag}', score: 20}}), (:IntNode {{tag: '{tag}', score: 30}})");

            var rows = new List<TypedScoreRow>();
            await foreach (var row in client.ExecuteRowsAsync<TypedScoreRow>(
                               $"MATCH (n:IntNode {{tag: '{tag}'}}) RETURN n.score AS score ORDER BY n.score"))
                rows.Add(row);

            await Assert.That(rows.Count).IsEqualTo(3);
            await Assert.That(rows[0].Score).IsEqualTo(10);
            await Assert.That(rows[1].Score).IsEqualTo(20);
            await Assert.That(rows[2].Score).IsEqualTo(30);
        });
    }

    [Test]
    [CombinedDataSources]
    public async Task ExecuteRowsAsync_TypedCompositeRow_StreamsAllFields(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var key = UniqueValue("typed-composite");
            var name = UniqueValue("person");
            using var _ = await client.ExecuteAsync(
                $"CREATE (:CompositePerson {{key: '{key}', name: '{name}', age: 42, active: true}})");

            var rows = new List<TypedPersonRow>();
            await foreach (var row in client.ExecuteRowsAsync<TypedPersonRow>(
                               $"MATCH (n:CompositePerson {{key: '{key}'}}) RETURN n.name AS name, n.age AS age, n.active AS active"))
                rows.Add(row);

            await Assert.That(rows.Count).IsEqualTo(1);
            await Assert.That(rows[0].Name).IsEqualTo(name);
            await Assert.That(rows[0].Age).IsEqualTo(42);
            await Assert.That(rows[0].Active).IsTrue();
        });
    }

    // ── DTOs ───────────────────────────────────────────────────────────────────

    public sealed class TypedNameRow
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;
    }

    public sealed class TypedScoreRow
    {
        [JsonPropertyName("score")]
        public int Score { get; init; }
    }

    public sealed class TypedPersonRow
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("age")]
        public int Age { get; init; }

        [JsonPropertyName("active")]
        public bool Active { get; init; }
    }
}

[JsonSerializable(typeof(TypedExtensionsIntegrationTests.TypedNameRow))]
[JsonSerializable(typeof(TypedExtensionsIntegrationTests.TypedScoreRow))]
[JsonSerializable(typeof(TypedExtensionsIntegrationTests.TypedPersonRow))]
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
internal partial class TypedExtensionsJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
