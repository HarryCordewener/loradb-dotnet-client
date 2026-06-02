using System.Text.Json.Serialization;

namespace LoraDb.Client.Models;

/// <summary>Query plan returned by <c>POST /explain</c>.</summary>
/// <remarks>
/// <see cref="Tree"/> node <see cref="LoraDbPlanNode.Details"/> values are opaque
/// human-readable strings; do not parse them programmatically.
/// <see cref="LoraDbPlanNode.EstimatedRows"/> is reserved for a future cost model
/// and is always <c>null</c> today.
/// </remarks>
public sealed class LoraDbQueryPlan
{
    /// <summary>The Cypher source text that was compiled.</summary>
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    /// <summary><c>"readOnly"</c> or <c>"mutating"</c>.</summary>
    [JsonPropertyName("shape")]
    public string Shape { get; set; } = string.Empty;

    /// <summary>Projected output columns in order.</summary>
    [JsonPropertyName("resultColumns")]
    public IReadOnlyList<string> ResultColumns { get; set; } = [];

    /// <summary>Root node of the physical plan tree.</summary>
    [JsonPropertyName("tree")]
    public LoraDbPlanNode Tree { get; set; } = null!;

    /// <summary>Returns <c>true</c> when <see cref="Shape"/> is <c>"readOnly"</c>.</summary>
    [JsonIgnore]
    public bool IsReadOnly => Shape == "readOnly";
}

/// <summary>A single node in a query plan tree.</summary>
public sealed class LoraDbPlanNode
{
    /// <summary>Physical-node id used as a key in <see cref="LoraDbQueryMetrics.PerOperator"/>.</summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>Name of the physical operator, e.g. <c>"NodeByLabelScan"</c>.</summary>
    [JsonPropertyName("operator")]
    public string Operator { get; set; } = string.Empty;

    /// <summary>Opaque human-readable operator details; do not parse programmatically.</summary>
    [JsonPropertyName("details")]
    public IReadOnlyDictionary<string, string> Details { get; set; } = new Dictionary<string, string>();

    /// <summary>Reserved for a future cost model; always <c>null</c> today.</summary>
    [JsonPropertyName("estimatedRows")]
    public long? EstimatedRows { get; set; }

    /// <summary>Child plan nodes.</summary>
    [JsonPropertyName("children")]
    public IReadOnlyList<LoraDbPlanNode> Children { get; set; } = [];
}
