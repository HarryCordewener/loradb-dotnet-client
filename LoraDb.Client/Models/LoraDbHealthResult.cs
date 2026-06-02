using System.Text.Json.Serialization;

namespace LoraDb.Client.Models;

/// <summary>Health probe result returned by <c>GET /health</c>.</summary>
public sealed class LoraDbHealthResult
{
    /// <summary>Server liveness status — <c>"ok"</c> when the process is running.</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>Returns <c>true</c> when <see cref="Status"/> equals <c>"ok"</c>.</summary>
    [JsonIgnore]
    public bool IsHealthy => Status == "ok";
}
