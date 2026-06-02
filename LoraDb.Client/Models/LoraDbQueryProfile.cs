using System.Text.Json.Serialization;

namespace LoraDb.Client.Models;

/// <summary>
/// Query execution profile returned by <c>POST /profile</c>.
/// The query runs for real; mutations produce the same side-effects as a normal
/// <c>POST /query</c> call.
/// </summary>
public sealed class LoraDbQueryProfile
{
    /// <summary>Physical query plan (same shape as <c>POST /explain</c>).</summary>
    [JsonPropertyName("plan")]
    public LoraDbQueryPlan Plan { get; set; } = null!;

    /// <summary>Runtime metrics collected during execution.</summary>
    [JsonPropertyName("metrics")]
    public LoraDbQueryMetrics Metrics { get; set; } = null!;
}

/// <summary>Runtime metrics for a profiled query execution.</summary>
public sealed class LoraDbQueryMetrics
{
    /// <summary>Total wall-clock time in nanoseconds.</summary>
    [JsonPropertyName("totalElapsedNs")]
    public long TotalElapsedNs { get; set; }

    /// <summary>Total number of rows emitted by the root operator.</summary>
    [JsonPropertyName("totalRows")]
    public long TotalRows { get; set; }

    /// <summary><c>true</c> when the query wrote to the graph.</summary>
    [JsonPropertyName("mutated")]
    public bool Mutated { get; set; }

    /// <summary>
    /// Per-operator metrics keyed by physical-node id (as string) matching
    /// <see cref="LoraDbPlanNode.Id"/> values in the plan tree.
    /// </summary>
    [JsonPropertyName("perOperator")]
    public IReadOnlyDictionary<string, LoraDbOperatorMetrics> PerOperator { get; set; } =
        new Dictionary<string, LoraDbOperatorMetrics>();
}

/// <summary>Runtime metrics for a single physical plan operator.</summary>
public sealed class LoraDbOperatorMetrics
{
    /// <summary>Number of rows this operator produced.</summary>
    [JsonPropertyName("rows")]
    public long Rows { get; set; }

    /// <summary>Reserved for a future phase; always <c>0</c> today.</summary>
    [JsonPropertyName("dbHits")]
    public long DbHits { get; set; }

    /// <summary>
    /// Wall-clock time inclusive of all descendant operators, in nanoseconds.
    /// </summary>
    [JsonPropertyName("elapsedNs")]
    public long ElapsedNs { get; set; }

    /// <summary>Number of times the pull-based iterator was advanced.</summary>
    [JsonPropertyName("nextCalls")]
    public long NextCalls { get; set; }
}
