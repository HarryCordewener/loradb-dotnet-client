using System.Text.Json.Serialization;

namespace LoraDb.Client.Models;

public sealed class LoraDbQueryRequest
{
    public const string DefaultFormat = "rows";

    [JsonPropertyName("query")]
    public required string Query { get; init; }

    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, object?>? Parameters { get; init; }

    [JsonPropertyName("format")]
    public string Format { get; init; } = DefaultFormat;
}
