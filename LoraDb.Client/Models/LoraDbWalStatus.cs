using System.Text.Json.Serialization;

namespace LoraDb.Client.Models;

/// <summary>WAL state returned by <c>POST /admin/wal/status</c>.</summary>
public sealed class LoraDbWalStatus
{
    /// <summary>
    /// Highest LSN known durable on disk.  In <c>none</c> sync mode this is only a
    /// logical checkpoint fence.
    /// </summary>
    [JsonPropertyName("durableLsn")]
    public long DurableLsn { get; set; }

    /// <summary>Next LSN the WAL will allocate.</summary>
    [JsonPropertyName("nextLsn")]
    public long NextLsn { get; set; }

    /// <summary>Numeric id of the segment currently accepting appends.</summary>
    [JsonPropertyName("activeSegmentId")]
    public long ActiveSegmentId { get; set; }

    /// <summary>Numeric id of the oldest retained segment.</summary>
    [JsonPropertyName("oldestSegmentId")]
    public long OldestSegmentId { get; set; }

    /// <summary>
    /// Latched background fsync failure message; <c>null</c> when GroupSync is healthy.
    /// </summary>
    [JsonPropertyName("bgFailure")]
    public string? BgFailure { get; set; }
}
