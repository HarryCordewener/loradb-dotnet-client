using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LoraDb.Client.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="LoraDbEmbeddedManagementClient.ExplainAsync"/> and
/// <see cref="LoraDbEmbeddedManagementClient.ProfileAsync"/> against an embedded LoraDB instance.
/// Each test opens its own transient (in-memory) database so tests are fully isolated.
/// </summary>
public class ExplainProfileIntegrationTests
{
    // ── ExplainAsync ───────────────────────────────────────────────────────────

    [Test]
    public async Task ExplainAsync_ReadOnlyMatchQuery_ReturnsPlanWithReadOnlyShape()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;
        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        await using var client = LoraDbEmbeddedManagementClient.Create(
            new LoraDbEmbeddedOpenOptions { NativeLibraryName = ffiLibraryPath });

        var plan = await client.ExplainAsync("MATCH (n:Person) RETURN n.name AS name");

        await Assert.That(plan).IsNotNull();
        await Assert.That(plan.Query).IsNotEmpty();
        await Assert.That(plan.IsReadOnly).IsTrue();
        await Assert.That(plan.Tree).IsNotNull();
        await Assert.That(plan.Tree.Operator).IsNotEmpty();
    }

    [Test]
    public async Task ExplainAsync_ReturnLiteral_ReturnsPlanWithResultColumns()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;
        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        await using var client = LoraDbEmbeddedManagementClient.Create(
            new LoraDbEmbeddedOpenOptions { NativeLibraryName = ffiLibraryPath });

        var plan = await client.ExplainAsync("RETURN 42 AS answer");

        await Assert.That(plan).IsNotNull();
        await Assert.That(plan.IsReadOnly).IsTrue();
        await Assert.That(plan.ResultColumns).Contains("answer");
    }

    [Test]
    public async Task ExplainAsync_WritingQuery_ReturnsMutatingShape()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;
        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        await using var client = LoraDbEmbeddedManagementClient.Create(
            new LoraDbEmbeddedOpenOptions { NativeLibraryName = ffiLibraryPath });

        var plan = await client.ExplainAsync(
            $"CREATE (n:ExplainNode {{key: '{Guid.NewGuid():N}'}}) RETURN n");

        await Assert.That(plan).IsNotNull();
        await Assert.That(plan.IsReadOnly).IsFalse();
        await Assert.That(plan.Tree).IsNotNull();
    }

    [Test]
    public async Task ExplainAsync_WithParameters_ReturnsPlanWithoutExecuting()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;
        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        await using var client = LoraDbEmbeddedManagementClient.Create(
            new LoraDbEmbeddedOpenOptions { NativeLibraryName = ffiLibraryPath });

        var plan = await client.ExplainAsync(
            "MATCH (n:Person) WHERE n.name = $name RETURN n.name AS name",
            parameters: new Dictionary<string, object?> { ["name"] = "Alice" });

        await Assert.That(plan).IsNotNull();
        await Assert.That(plan.Tree).IsNotNull();

        // ExplainAsync must not execute the query; no nodes should have been created.
        using var countResult = await client.ExecuteAsync("MATCH (n:Person) RETURN count(n) AS total");
        var total = countResult.Root.GetProperty("rows")[0].GetProperty("total").GetInt32();
        await Assert.That(total).IsEqualTo(0);
    }

    [Test]
    public async Task ExplainAsync_PlanTree_HasAtLeastOneOperator()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;
        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        await using var client = LoraDbEmbeddedManagementClient.Create(
            new LoraDbEmbeddedOpenOptions { NativeLibraryName = ffiLibraryPath });

        var plan = await client.ExplainAsync("MATCH (n) RETURN count(n) AS total");

        // Walk the tree and collect all operator names.
        var operators = new List<string>();
        Traverse(plan.Tree, operators);

        await Assert.That(operators.Count).IsGreaterThan(0);
        await Assert.That(operators.All(op => !string.IsNullOrEmpty(op))).IsTrue();
    }

    // ── ProfileAsync ───────────────────────────────────────────────────────────

    [Test]
    public async Task ProfileAsync_ReturnLiteral_ReturnsMetricsWithNonNegativeElapsedTime()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;
        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        await using var client = LoraDbEmbeddedManagementClient.Create(
            new LoraDbEmbeddedOpenOptions { NativeLibraryName = ffiLibraryPath });

        var profile = await client.ProfileAsync("RETURN 1 AS n");

        await Assert.That(profile).IsNotNull();
        await Assert.That(profile.Plan).IsNotNull();
        await Assert.That(profile.Metrics).IsNotNull();
        await Assert.That(profile.Metrics.TotalElapsedNs).IsGreaterThanOrEqualTo(0);
        await Assert.That(profile.Metrics.Mutated).IsFalse();
    }

    [Test]
    public async Task ProfileAsync_ReadOnlyMatch_ReportsNotMutated()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;
        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        await using var client = LoraDbEmbeddedManagementClient.Create(
            new LoraDbEmbeddedOpenOptions { NativeLibraryName = ffiLibraryPath });

        var profile = await client.ProfileAsync("MATCH (n) RETURN count(n) AS total");

        await Assert.That(profile.Metrics.Mutated).IsFalse();
    }

    [Test]
    public async Task ProfileAsync_WritingQuery_ReportsMutated()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;
        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        await using var client = LoraDbEmbeddedManagementClient.Create(
            new LoraDbEmbeddedOpenOptions { NativeLibraryName = ffiLibraryPath });

        var profile = await client.ProfileAsync(
            $"CREATE (:ProfileNode {{key: '{Guid.NewGuid():N}'}})");

        await Assert.That(profile.Metrics.Mutated).IsTrue();
    }

    [Test]
    public async Task ProfileAsync_QueryOnRealData_ReturnsCorrectTotalRows()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;
        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        var tag = $"profile_{Guid.NewGuid():N}";

        await using var client = LoraDbEmbeddedManagementClient.Create(
            new LoraDbEmbeddedOpenOptions { NativeLibraryName = ffiLibraryPath });

        using var _ = await client.ExecuteAsync(
            $"CREATE (:ProfilePerson {{tag: '{tag}', name: '{Guid.NewGuid():N}'}}), " +
            $"       (:ProfilePerson {{tag: '{tag}', name: '{Guid.NewGuid():N}'}}), " +
            $"       (:ProfilePerson {{tag: '{tag}', name: '{Guid.NewGuid():N}'}})");

        var profile = await client.ProfileAsync(
            $"MATCH (n:ProfilePerson {{tag: '{tag}'}}) RETURN n.name AS name");

        await Assert.That(profile.Metrics.TotalRows).IsEqualTo(3);
    }

    [Test]
    public async Task ProfileAsync_IncludesPlanWithOperatorTree()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;
        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        await using var client = LoraDbEmbeddedManagementClient.Create(
            new LoraDbEmbeddedOpenOptions { NativeLibraryName = ffiLibraryPath });

        var profile = await client.ProfileAsync("MATCH (n) RETURN count(n) AS total");

        await Assert.That(profile.Plan).IsNotNull();
        await Assert.That(profile.Plan.Tree).IsNotNull();
        await Assert.That(profile.Plan.Tree.Operator).IsNotEmpty();
    }

    [Test]
    public async Task ProfileAsync_WithParameters_ExecutesCorrectly()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;
        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        var tag = $"profile_param_{Guid.NewGuid():N}";

        await using var client = LoraDbEmbeddedManagementClient.Create(
            new LoraDbEmbeddedOpenOptions { NativeLibraryName = ffiLibraryPath });

        using var _ = await client.ExecuteAsync(
            $"CREATE (:ParamProfileNode {{tag: '{tag}', value: 7}}), " +
            $"       (:ParamProfileNode {{tag: '{tag}', value: 14}})");

        var profile = await client.ProfileAsync(
            "MATCH (n:ParamProfileNode {tag: $tag}) WHERE n.value > $min RETURN n.value AS value",
            parameters: new Dictionary<string, object?> { ["tag"] = tag, ["min"] = 10 });

        await Assert.That(profile.Metrics.TotalRows).IsEqualTo(1);
    }

    [Test]
    public async Task ProfileAsync_PerOperatorMetrics_NotEmpty()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;
        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        await using var client = LoraDbEmbeddedManagementClient.Create(
            new LoraDbEmbeddedOpenOptions { NativeLibraryName = ffiLibraryPath });

        var profile = await client.ProfileAsync("RETURN 1 AS x, 2 AS y");

        await Assert.That(profile.Metrics.PerOperator).IsNotNull();
        await Assert.That(profile.Metrics.PerOperator.Count).IsGreaterThan(0);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static void Traverse(Models.LoraDbPlanNode node, List<string> operators)
    {
        operators.Add(node.Operator);
        foreach (var child in node.Children)
            Traverse(child, operators);
    }
}
