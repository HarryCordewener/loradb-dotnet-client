using System.Text.Json.Serialization;

namespace LoraDb.Client.Models;

public sealed class LoraDbQueryRequest
{
    public const string DefaultFormat = "rows";

    public LoraDbQueryRequest(string query)
    {
        Query = query ?? throw new ArgumentNullException(nameof(query));
    }

    [JsonPropertyName("query")]
    public string Query { get; set; }

    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, object?>? Parameters { get; set; }

    [JsonPropertyName("format")]
    public string Format { get; set; } = DefaultFormat;
}
