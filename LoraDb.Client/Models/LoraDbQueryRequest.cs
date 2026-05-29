using System.Text.Json.Serialization;

namespace LoraDb.Client.Models;

public sealed class LoraDbQueryRequest
{
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    [JsonPropertyName("params")]
    public IReadOnlyDictionary<string, object?>? Parameters { get; init; }

    [JsonPropertyName("format")]
    public string Format { get; init; } = "rows";
}
