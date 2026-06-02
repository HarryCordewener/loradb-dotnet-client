using System.Net;
using System.Text;
using System.Text.Json;
using LoraDb.Client.Tests.Helpers;
using TUnit.Assertions.Extensions;

namespace LoraDb.Client.Tests;

/// <summary>
/// Unit tests for <see cref="LoraDbHttpManagementClient"/> and the management
/// methods it exposes.  All tests use <see cref="RecordingHttpHandler"/> — no
/// real server is required.
/// </summary>
public class HttpManagementClientTests
{
    private static readonly Uri Endpoint = new("http://localhost:4747/");

    // ── Factory / lifecycle ────────────────────────────────────────────────────

    [Test]
    public async Task Create_ThrowsForNullEndpoint()
    {
        await Assert.That(() =>
        {
            LoraDbHttpManagementClient.Create(null!);
            return Task.CompletedTask;
        })
            .ThrowsException()
            .And
            .IsTypeOf<ArgumentNullException>();
    }

    [Test]
    public async Task Create_WithNullFactory_ThrowsArgumentNullException()
    {
        await Assert.That(() =>
        {
            LoraDbHttpManagementClient.Create(Endpoint, (IHttpClientFactory)null!);
            return Task.CompletedTask;
        })
            .ThrowsException()
            .And
            .IsTypeOf<ArgumentNullException>();
    }

    [Test]
    public async Task Create_WithFactory_AndNullEndpoint_Throws()
    {
        var factory = new FakeHttpClientFactory(new HttpClient());
        await Assert.That(() =>
        {
            LoraDbHttpManagementClient.Create(null!, factory);
            return Task.CompletedTask;
        })
            .ThrowsException()
            .And
            .IsTypeOf<ArgumentNullException>();
    }

    [Test]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        var handler = RecordingHttpHandler.WithJson("""{"rows":[]}""");
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));
        await client.DisposeAsync();
        // Should not throw
    }

    // ── ExecuteAsync ───────────────────────────────────────────────────────────

    [Test]
    public async Task ExecuteAsync_PostsToQueryPath()
    {
        var handler = RecordingHttpHandler.WithJson("""{"rows":[{"name":"Alice"}]}""");
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        using var result = await client.ExecuteAsync("MATCH (u:User) RETURN u.name AS name");

        await Assert.That(handler.LastRequest!.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(handler.LastRequest.RequestUri!.AbsolutePath).IsEqualTo("/query");
    }

    [Test]
    public async Task ExecuteAsync_ThrowsForEmptyQuery()
    {
        var handler = RecordingHttpHandler.WithJson("""{"rows":[]}""");
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        var ex = await Assert.That(async () => await client.ExecuteAsync("  "))
            .ThrowsException()
            .And
            .IsTypeOf<ArgumentException>();

        await Assert.That(ex!.ParamName).IsEqualTo("query");
    }

    // ── HealthAsync ────────────────────────────────────────────────────────────

    [Test]
    public async Task HealthAsync_GetRequest_ToHealthPath()
    {
        var handler = RecordingHttpHandler.WithJson("""{"status":"ok"}""");
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        var result = await client.HealthAsync();

        await Assert.That(handler.LastRequest!.Method).IsEqualTo(HttpMethod.Get);
        await Assert.That(handler.LastRequest.RequestUri!.AbsolutePath).IsEqualTo("/health");
        await Assert.That(result.Status).IsEqualTo("ok");
        await Assert.That(result.IsHealthy).IsTrue();
    }

    [Test]
    public async Task HealthAsync_UnhealthyStatus_IsHealthyFalse()
    {
        var handler = RecordingHttpHandler.WithJson("""{"status":"degraded"}""");
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        var result = await client.HealthAsync();

        await Assert.That(result.Status).IsEqualTo("degraded");
        await Assert.That(result.IsHealthy).IsFalse();
    }

    [Test]
    public async Task HealthAsync_ServerError_ThrowsHttpRequestException()
    {
        var handler = RecordingHttpHandler.WithStatus(HttpStatusCode.ServiceUnavailable);
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        await Assert.That(async () => await client.HealthAsync())
            .ThrowsException()
            .WithMessageContaining("503");
    }

    // ── ExplainAsync ───────────────────────────────────────────────────────────

    private static string SampleExplainJson(string query = "MATCH (p:Person) RETURN p") => $$"""
        {
          "query": "{{query}}",
          "shape": "readOnly",
          "resultColumns": ["p"],
          "tree": {
            "id": 1,
            "operator": "NodeByLabelScan",
            "details": {"var": "p", "labels": "Person"},
            "estimatedRows": null,
            "children": []
          }
        }
        """;

    [Test]
    public async Task ExplainAsync_PostsToExplainPath()
    {
        var handler = RecordingHttpHandler.WithJson(SampleExplainJson());
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        var plan = await client.ExplainAsync("MATCH (p:Person) RETURN p");

        await Assert.That(handler.LastRequest!.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(handler.LastRequest.RequestUri!.AbsolutePath).IsEqualTo("/explain");
    }

    [Test]
    public async Task ExplainAsync_SendsQueryInBody()
    {
        var handler = RecordingHttpHandler.WithJson(SampleExplainJson("MATCH (n) RETURN n"));
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        await client.ExplainAsync("MATCH (n) RETURN n");

        using var doc = JsonDocument.Parse(handler.LastRequestJson!);
        await Assert.That(doc.RootElement.GetProperty("query").GetString())
            .IsEqualTo("MATCH (n) RETURN n");
    }

    [Test]
    public async Task ExplainAsync_SendsParametersInBody()
    {
        var handler = RecordingHttpHandler.WithJson(SampleExplainJson());
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        await client.ExplainAsync("MATCH (p:Person) WHERE p.name = $name RETURN p",
            new Dictionary<string, object?> { ["name"] = "Alice" });

        using var doc = JsonDocument.Parse(handler.LastRequestJson!);
        await Assert.That(doc.RootElement.GetProperty("params").GetProperty("name").GetString())
            .IsEqualTo("Alice");
    }

    [Test]
    public async Task ExplainAsync_WithNullParameters_OmitsParamsField()
    {
        var handler = RecordingHttpHandler.WithJson(SampleExplainJson());
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        await client.ExplainAsync("MATCH (p:Person) RETURN p", null);

        using var doc = JsonDocument.Parse(handler.LastRequestJson!);
        await Assert.That(doc.RootElement.TryGetProperty("params", out _)).IsFalse();
    }

    [Test]
    public async Task ExplainAsync_DeserializesQueryPlan()
    {
        var handler = RecordingHttpHandler.WithJson(SampleExplainJson("MATCH (p:Person) RETURN p"));
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        var plan = await client.ExplainAsync("MATCH (p:Person) RETURN p");

        await Assert.That(plan.Query).IsEqualTo("MATCH (p:Person) RETURN p");
        await Assert.That(plan.Shape).IsEqualTo("readOnly");
        await Assert.That(plan.IsReadOnly).IsTrue();
        await Assert.That(plan.ResultColumns).Contains("p");
        await Assert.That(plan.Tree).IsNotNull();
        await Assert.That(plan.Tree.Id).IsEqualTo(1);
        await Assert.That(plan.Tree.Operator).IsEqualTo("NodeByLabelScan");
        await Assert.That(plan.Tree.Children).IsEmpty();
    }

    [Test]
    public async Task ExplainAsync_ThrowsForEmptyQuery()
    {
        var handler = RecordingHttpHandler.WithJson(SampleExplainJson());
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        var ex = await Assert.That(async () => await client.ExplainAsync("  "))
            .ThrowsException()
            .And
            .IsTypeOf<ArgumentException>();

        await Assert.That(ex!.ParamName).IsEqualTo("query");
    }

    [Test]
    public async Task ExplainAsync_MutatingQuery_ShapeIsMutating()
    {
        var mutatingJson = """
            {
              "query": "CREATE (:Person {name: $name})",
              "shape": "mutating",
              "resultColumns": [],
              "tree": {
                "id": 1,
                "operator": "Create",
                "details": {},
                "estimatedRows": null,
                "children": []
              }
            }
            """;
        var handler = RecordingHttpHandler.WithJson(mutatingJson);
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        var plan = await client.ExplainAsync("CREATE (:Person {name: $name})");

        await Assert.That(plan.IsReadOnly).IsFalse();
        await Assert.That(plan.Shape).IsEqualTo("mutating");
    }

    // ── ProfileAsync ───────────────────────────────────────────────────────────

    private static string SampleProfileJson() => """
        {
          "plan": {
            "query": "MATCH (p:Person) RETURN p",
            "shape": "readOnly",
            "resultColumns": ["p"],
            "tree": {
              "id": 2,
              "operator": "Projection",
              "details": {"items": "p"},
              "estimatedRows": null,
              "children": [
                {
                  "id": 1,
                  "operator": "NodeByLabelScan",
                  "details": {"var": "v0", "labels": "Person"},
                  "estimatedRows": null,
                  "children": []
                }
              ]
            }
          },
          "metrics": {
            "totalElapsedNs": 124500,
            "totalRows": 3,
            "mutated": false,
            "perOperator": {
              "1": {"rows": 5, "dbHits": 0, "elapsedNs": 18200, "nextCalls": 6},
              "2": {"rows": 4, "dbHits": 0, "elapsedNs": 21100, "nextCalls": 5}
            }
          }
        }
        """;

    [Test]
    public async Task ProfileAsync_PostsToProfilePath()
    {
        var handler = RecordingHttpHandler.WithJson(SampleProfileJson());
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        await client.ProfileAsync("MATCH (p:Person) RETURN p");

        await Assert.That(handler.LastRequest!.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(handler.LastRequest.RequestUri!.AbsolutePath).IsEqualTo("/profile");
    }

    [Test]
    public async Task ProfileAsync_SendsQueryInBody()
    {
        var handler = RecordingHttpHandler.WithJson(SampleProfileJson());
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        await client.ProfileAsync("MATCH (p:Person) RETURN p");

        using var doc = JsonDocument.Parse(handler.LastRequestJson!);
        await Assert.That(doc.RootElement.GetProperty("query").GetString())
            .IsEqualTo("MATCH (p:Person) RETURN p");
    }

    [Test]
    public async Task ProfileAsync_DeserializesProfile()
    {
        var handler = RecordingHttpHandler.WithJson(SampleProfileJson());
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        var profile = await client.ProfileAsync("MATCH (p:Person) RETURN p");

        await Assert.That(profile.Plan).IsNotNull();
        await Assert.That(profile.Plan.Shape).IsEqualTo("readOnly");
        await Assert.That(profile.Metrics).IsNotNull();
        await Assert.That(profile.Metrics.TotalElapsedNs).IsEqualTo(124500L);
        await Assert.That(profile.Metrics.TotalRows).IsEqualTo(3L);
        await Assert.That(profile.Metrics.Mutated).IsFalse();
        await Assert.That(profile.Metrics.PerOperator).ContainsKey("1");
        await Assert.That(profile.Metrics.PerOperator["1"].Rows).IsEqualTo(5L);
        await Assert.That(profile.Metrics.PerOperator["1"].ElapsedNs).IsEqualTo(18200L);
        await Assert.That(profile.Metrics.PerOperator["1"].NextCalls).IsEqualTo(6L);
    }

    [Test]
    public async Task ProfileAsync_NestedPlanTree_DeserializesChildren()
    {
        var handler = RecordingHttpHandler.WithJson(SampleProfileJson());
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        var profile = await client.ProfileAsync("MATCH (p:Person) RETURN p");

        await Assert.That(profile.Plan.Tree.Id).IsEqualTo(2);
        await Assert.That(profile.Plan.Tree.Children).Count().IsEqualTo(1);
        await Assert.That(profile.Plan.Tree.Children[0].Id).IsEqualTo(1);
        await Assert.That(profile.Plan.Tree.Children[0].Operator).IsEqualTo("NodeByLabelScan");
    }

    [Test]
    public async Task ProfileAsync_ThrowsForEmptyQuery()
    {
        var handler = RecordingHttpHandler.WithJson(SampleProfileJson());
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        var ex = await Assert.That(async () => await client.ProfileAsync(""))
            .ThrowsException()
            .And
            .IsTypeOf<ArgumentException>();

        await Assert.That(ex!.ParamName).IsEqualTo("query");
    }

    // ── SaveSnapshotAsync ──────────────────────────────────────────────────────

    private static string SampleSnapshotJson(string path = "/var/lib/lora/db.bin") => $$"""
        {
          "formatVersion": 1,
          "nodeCount": 1024,
          "relationshipCount": 4096,
          "walLsn": null,
          "path": "{{path}}"
        }
        """;

    [Test]
    public async Task SaveSnapshotAsync_PostsToCorrectPath_NoBody()
    {
        var handler = RecordingHttpHandler.WithJson(SampleSnapshotJson());
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        await client.SaveSnapshotAsync();

        await Assert.That(handler.LastRequest!.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(handler.LastRequest.RequestUri!.AbsolutePath).IsEqualTo("/admin/snapshot/save");
        await Assert.That(handler.LastRequest.Content).IsNull();
    }

    [Test]
    public async Task SaveSnapshotAsync_WithPath_SendsPathInBody()
    {
        var handler = RecordingHttpHandler.WithJson(SampleSnapshotJson("/custom/path.bin"));
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        await client.SaveSnapshotAsync("/custom/path.bin");

        using var doc = JsonDocument.Parse(handler.LastRequestJson!);
        await Assert.That(doc.RootElement.GetProperty("path").GetString())
            .IsEqualTo("/custom/path.bin");
    }

    [Test]
    public async Task SaveSnapshotAsync_DeserializesSnapshotMeta()
    {
        var handler = RecordingHttpHandler.WithJson(SampleSnapshotJson());
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        var meta = await client.SaveSnapshotAsync();

        await Assert.That(meta.FormatVersion).IsEqualTo(1);
        await Assert.That(meta.NodeCount).IsEqualTo(1024L);
        await Assert.That(meta.RelationshipCount).IsEqualTo(4096L);
        await Assert.That(meta.WalLsn).IsNull();
        await Assert.That(meta.Path).IsEqualTo("/var/lib/lora/db.bin");
    }

    [Test]
    public async Task SaveSnapshotAsync_NotMounted_ThrowsHttpRequestException()
    {
        var handler = RecordingHttpHandler.WithStatus(HttpStatusCode.NotFound);
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        await Assert.That(async () => await client.SaveSnapshotAsync())
            .ThrowsException()
            .WithMessageContaining("404");
    }

    // ── LoadSnapshotAsync ──────────────────────────────────────────────────────

    [Test]
    public async Task LoadSnapshotAsync_PostsToCorrectPath()
    {
        var handler = RecordingHttpHandler.WithJson(SampleSnapshotJson());
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        await client.LoadSnapshotAsync();

        await Assert.That(handler.LastRequest!.RequestUri!.AbsolutePath).IsEqualTo("/admin/snapshot/load");
    }

    [Test]
    public async Task LoadSnapshotAsync_WithPath_SendsPathInBody()
    {
        var handler = RecordingHttpHandler.WithJson(SampleSnapshotJson());
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        await client.LoadSnapshotAsync("/restore/path.bin");

        using var doc = JsonDocument.Parse(handler.LastRequestJson!);
        await Assert.That(doc.RootElement.GetProperty("path").GetString())
            .IsEqualTo("/restore/path.bin");
    }

    // ── CheckpointAsync ────────────────────────────────────────────────────────

    private static string SampleCheckpointJson() => """
        {
          "formatVersion": 1,
          "nodeCount": 512,
          "relationshipCount": 256,
          "walLsn": 4815,
          "path": "/var/lib/lora/checkpoint.bin"
        }
        """;

    [Test]
    public async Task CheckpointAsync_PostsToCorrectPath()
    {
        var handler = RecordingHttpHandler.WithJson(SampleCheckpointJson());
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        await client.CheckpointAsync();

        await Assert.That(handler.LastRequest!.RequestUri!.AbsolutePath).IsEqualTo("/admin/checkpoint");
    }

    [Test]
    public async Task CheckpointAsync_DeserializesWalLsn()
    {
        var handler = RecordingHttpHandler.WithJson(SampleCheckpointJson());
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        var meta = await client.CheckpointAsync();

        await Assert.That(meta.WalLsn).IsEqualTo(4815L);
    }

    [Test]
    public async Task CheckpointAsync_WithPath_SendsPathInBody()
    {
        var handler = RecordingHttpHandler.WithJson(SampleCheckpointJson());
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        await client.CheckpointAsync("/custom/checkpoint.bin");

        using var doc = JsonDocument.Parse(handler.LastRequestJson!);
        await Assert.That(doc.RootElement.GetProperty("path").GetString())
            .IsEqualTo("/custom/checkpoint.bin");
    }

    // ── WalStatusAsync ─────────────────────────────────────────────────────────

    private static string SampleWalStatusJson(string? bgFailure = null) => $$"""
        {
          "durableLsn": 4815,
          "nextLsn": 4820,
          "activeSegmentId": 3,
          "oldestSegmentId": 2,
          "bgFailure": {{(bgFailure is null ? "null" : $"\"{bgFailure}\"")}}
        }
        """;

    [Test]
    public async Task WalStatusAsync_PostsToCorrectPath_NoBody()
    {
        var handler = RecordingHttpHandler.WithJson(SampleWalStatusJson());
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        await client.WalStatusAsync();

        await Assert.That(handler.LastRequest!.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(handler.LastRequest.RequestUri!.AbsolutePath).IsEqualTo("/admin/wal/status");
        await Assert.That(handler.LastRequest.Content).IsNull();
    }

    [Test]
    public async Task WalStatusAsync_DeserializesStatus()
    {
        var handler = RecordingHttpHandler.WithJson(SampleWalStatusJson());
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        var status = await client.WalStatusAsync();

        await Assert.That(status.DurableLsn).IsEqualTo(4815L);
        await Assert.That(status.NextLsn).IsEqualTo(4820L);
        await Assert.That(status.ActiveSegmentId).IsEqualTo(3L);
        await Assert.That(status.OldestSegmentId).IsEqualTo(2L);
        await Assert.That(status.BgFailure).IsNull();
    }

    [Test]
    public async Task WalStatusAsync_WithBgFailure_DeserializesMessage()
    {
        var handler = RecordingHttpHandler.WithJson(SampleWalStatusJson("fsync failed: disk full"));
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        var status = await client.WalStatusAsync();

        await Assert.That(status.BgFailure).IsEqualTo("fsync failed: disk full");
    }

    // ── TruncateWalAsync ───────────────────────────────────────────────────────

    [Test]
    public async Task TruncateWalAsync_PostsToCorrectPath_NoBody()
    {
        var handler = RecordingHttpHandler.WithStatus(HttpStatusCode.NoContent);
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        await client.TruncateWalAsync();

        await Assert.That(handler.LastRequest!.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(handler.LastRequest.RequestUri!.AbsolutePath).IsEqualTo("/admin/wal/truncate");
        await Assert.That(handler.LastRequest.Content).IsNull();
    }

    [Test]
    public async Task TruncateWalAsync_WithFenceLsn_SendsInBody()
    {
        var handler = RecordingHttpHandler.WithStatus(HttpStatusCode.NoContent);
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        await client.TruncateWalAsync(4815L);

        using var doc = JsonDocument.Parse(handler.LastRequestJson!);
        await Assert.That(doc.RootElement.GetProperty("fenceLsn").GetInt64()).IsEqualTo(4815L);
    }

    [Test]
    public async Task TruncateWalAsync_WithNullFenceLsn_OmitsFenceLsnField()
    {
        var handler = RecordingHttpHandler.WithStatus(HttpStatusCode.NoContent);
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        await client.TruncateWalAsync(null);

        await Assert.That(handler.LastRequest!.Content).IsNull();
    }

    [Test]
    public async Task TruncateWalAsync_NotMounted_ThrowsHttpRequestException()
    {
        var handler = RecordingHttpHandler.WithStatus(HttpStatusCode.NotFound);
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, handler.BuildClient(Endpoint));

        await Assert.That(async () => await client.TruncateWalAsync())
            .ThrowsException()
            .WithMessageContaining("404");
    }

    // ── Factory with IHttpClientFactory ───────────────────────────────────────

    [Test]
    public async Task Create_WithFactory_ExecutesQuery()
    {
        var handler = RecordingHttpHandler.WithJson("""{"rows":[{"name":"Alice"}]}""");
        var factory = new FakeHttpClientFactory(handler.BuildClient(Endpoint));
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, factory);

        using var result = await client.ExecuteAsync("MATCH (u:User) RETURN u.name AS name");

        await Assert.That(handler.CallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Create_WithFactory_UsesNamedClient()
    {
        var handler = RecordingHttpHandler.WithJson("""{"rows":[]}""");
        string? capturedName = null;
        var factory = new SpyHttpClientFactory(handler.BuildClient(Endpoint), n => capturedName = n);
        await using var client = LoraDbHttpManagementClient.Create(Endpoint, factory, "mgmt-client");

        using var _ = await client.ExecuteAsync("MATCH (n) RETURN n");

        await Assert.That(capturedName).IsEqualTo("mgmt-client");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class SpyHttpClientFactory(HttpClient client, Action<string> onCreateClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            onCreateClient(name);
            return client;
        }
    }
}
