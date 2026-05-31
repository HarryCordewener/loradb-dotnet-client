using System.Text.Json.Serialization;

namespace LoraDb.Client.Models;

/// <summary>
/// Snapshot metadata returned by <c>POST /admin/snapshot/save</c>,
/// <c>POST /admin/snapshot/load</c>, and <c>POST /admin/checkpoint</c>.
/// </summary>
public sealed class LoraDbSnapshotMeta
{
    /// <summary>Snapshot file format version (currently <c>1</c>).</summary>
    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; set; }

    /// <summary>Number of nodes in the saved/loaded graph.</summary>
    [JsonPropertyName("nodeCount")]
    public long NodeCount { get; set; }

    /// <summary>Number of relationships in the saved/loaded graph.</summary>
    [JsonPropertyName("relationshipCount")]
    public long RelationshipCount { get; set; }

    /// <summary>
    /// <c>null</c> for a pure snapshot; non-<c>null</c> for a checkpoint snapshot
    /// written with WAL enabled, containing the WAL's durable fence LSN.
    /// </summary>
    [JsonPropertyName("walLsn")]
    public long? WalLsn { get; set; }

    /// <summary>Filesystem path the server actually used.</summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
}
