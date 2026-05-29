using LoraDb.Client.IntegrationTests.Fixtures;
using LoraDb.Client.IntegrationTests.Helpers;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LoraDb.Client.IntegrationTests;

public class CrudIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task CreateNode_WithoutLabels_CanBeMatchedBack(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var key = UniqueValue("node");

            using var createResult = await client.ExecuteAsync($"CREATE (n {{key: '{key}'}}) RETURN n");
            var node = IntegrationAssertions.GetRowColumn(createResult, 0, "n");
            await Assert.That(node.GetProperty("labels").GetArrayLength()).IsEqualTo(0);

            using var matchResult = await client.ExecuteAsync($"MATCH (n {{key: '{key}'}}) RETURN n.key AS key");
            await AssertSingleStringResult(matchResult, "key", key);
        });
    }

    [Test]
    public async Task CreateNode_WithSingleLabel_CanBeMatchedBack(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var key = UniqueValue("person");

            using var createResult = await client.ExecuteAsync($"CREATE (n:Person {{key: '{key}'}}) RETURN n");
            var labels = IntegrationAssertions.GetRowColumn(createResult, 0, "n").GetProperty("labels");
            await Assert.That(labels[0].GetString()).IsEqualTo("Person");

            using var matchResult = await client.ExecuteAsync($"MATCH (n:Person {{key: '{key}'}}) RETURN n.key AS key");
            await AssertSingleStringResult(matchResult, "key", key);
        });
    }

    [Test]
    public async Task CreateNode_WithMultipleLabels_CanBeMatchedBack(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var key = UniqueValue("employee");

            using var createResult = await client.ExecuteAsync($"CREATE (n:Person:Employee {{key: '{key}'}}) RETURN n");
            var labels = IntegrationAssertions.GetRowColumn(createResult, 0, "n").GetProperty("labels");
            await Assert.That(labels.GetArrayLength()).IsEqualTo(2);

            using var matchResult = await client.ExecuteAsync($"MATCH (n:Person:Employee {{key: '{key}'}}) RETURN n.key AS key");
            await AssertSingleStringResult(matchResult, "key", key);
        });
    }

    [Test]
    public async Task CreateNode_WithScalarAndListProperties_RoundTripsValues(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var key = UniqueValue("props");

            using var createResult = await client.ExecuteAsync(
                $"CREATE (n:Thing {{key: '{key}', name: 'Widget', age: 42, score: 3.5, active: true, tags: ['red', 'blue']}}) RETURN n");
            var properties = IntegrationAssertions.GetRowColumn(createResult, 0, "n").GetProperty("properties");
            await Assert.That(properties.GetProperty("name").GetString()).IsEqualTo("Widget");
            await Assert.That(properties.GetProperty("age").GetInt32()).IsEqualTo(42);
            await Assert.That(properties.GetProperty("score").GetDouble()).IsEqualTo(3.5);
            await Assert.That(properties.GetProperty("active").GetBoolean()).IsTrue();
            await Assert.That(properties.GetProperty("tags").GetArrayLength()).IsEqualTo(2);
        });
    }

    [Test]
    public async Task CreateRelationship_WithProperties_CanBeMatchedBack(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var createResult = await client.ExecuteAsync(
                "MATCH (a:Person {name: 'Carol'}), (b:Person {name: 'Dave'}) CREATE (a)-[r:LIKES {since: 2024}]->(b) RETURN r");
            var relationship = IntegrationAssertions.GetRowColumn(createResult, 0, "r");
            await Assert.That(relationship.GetProperty("type").GetString()).IsEqualTo("LIKES");
            await Assert.That(relationship.GetProperty("properties").GetProperty("since").GetInt32()).IsEqualTo(2024);

            using var matchResult = await client.ExecuteAsync(
                "MATCH (:Person {name: 'Carol'})-[r:LIKES]->(:Person {name: 'Dave'}) RETURN r.since AS since");
            await IntegrationAssertions.AssertSingleIntegerResult(matchResult, "since", 2024);
        });
    }

    [Test]
    public async Task SetProperty_OnExistingNode_UpdatesValue(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (n:Person {name: 'Alice'}) SET n.age = 31 RETURN n.age AS age");
            await IntegrationAssertions.AssertSingleIntegerResult(result, "age", 31);
        });
    }

    [Test]
    public async Task RemoveProperty_OnExistingNode_RemovesField(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (n:Person {name: 'Alice'}) REMOVE n.tags RETURN n");
            var properties = IntegrationAssertions.GetRowColumn(result, 0, "n").GetProperty("properties");
            await Assert.That(properties.TryGetProperty("tags", out _)).IsFalse();
        });
    }

    [Test]
    public async Task SetLabel_OnExistingNode_AddsLabel(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var result = await client.ExecuteAsync(
                "MATCH (n:Person {name: 'Dave'}) SET n:VIP RETURN n");
            var labels = IntegrationAssertions.GetRowColumn(result, 0, "n").GetProperty("labels");
            await Assert.That(labels.EnumerateArray().Select(label => label.GetString()).Contains("VIP")).IsTrue();
        });
    }

    [Test]
    public async Task RemoveLabel_OnExistingNode_RemovesLabel(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            await using (var prepareClient = fixture.CreateClient())
            {
                using var prepareResult = await prepareClient.ExecuteAsync("MATCH (n:Person {name: 'Dave'}) SET n:VIP RETURN n");
            }

            using var result = await client.ExecuteAsync("MATCH (n:Person:VIP {name: 'Dave'}) REMOVE n:VIP RETURN n");
            var labels = IntegrationAssertions.GetRowColumn(result, 0, "n").GetProperty("labels");
            await Assert.That(labels.EnumerateArray().Select(label => label.GetString()).Contains("VIP")).IsFalse();
        });
    }

    [Test]
    public async Task DeleteNode_RemovesIsolatedNode(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithCleanDatabaseAsync(fixture, async client =>
        {
            var key = UniqueValue("temp");
            using var createResult = await client.ExecuteAsync($"CREATE (:Temp {{key: '{key}'}})");
            using var deleteResult = await client.ExecuteAsync($"MATCH (n:Temp {{key: '{key}'}}) DELETE n");
            using var verifyResult = await client.ExecuteAsync($"MATCH (n:Temp {{key: '{key}'}}) RETURN count(n) AS total");
            await IntegrationAssertions.AssertSingleIntegerResult(verifyResult, "total", 0);
        });
    }

    [Test]
    public async Task DetachDelete_RemovesConnectedNode(
        [ClassDataSource<EmbeddedClientFixture>(Shared = SharedType.PerAssembly)]
        [ClassDataSource<HttpClientFixture>(Shared = SharedType.PerAssembly)]
        ILoraDbClientFixture fixture)
    {
        await WithSocialGraphAsync(fixture, async client =>
        {
            using var deleteResult = await client.ExecuteAsync("MATCH (n:Person {name: 'Bob'}) DETACH DELETE n");
            using var verifyResult = await client.ExecuteAsync("MATCH (n:Person {name: 'Bob'}) RETURN count(n) AS total");
            await IntegrationAssertions.AssertSingleIntegerResult(verifyResult, "total", 0);
        });
    }
}
