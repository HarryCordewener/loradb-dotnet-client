using System.Text.Encodings.Web;
using System.Text.Json;

namespace LoraDb.Client.Serialization;

internal static class LoraDbJsonSerializerOptions
{
    public static readonly JsonSerializerOptions RequestSerializationOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Baseline options used when no custom options are supplied by the caller.
    /// Do not mutate this instance; use <see cref="CreateResultOptions"/> to obtain
    /// a fresh writable copy.
    /// </summary>
    public static readonly JsonSerializerOptions DefaultResultSerializationOptions =
        new(JsonSerializerDefaults.Web);

    public static JsonSerializerOptions CreateResultOptions(JsonSerializerOptions? serializerOptions)
    {
        return serializerOptions is null
            ? new JsonSerializerOptions(DefaultResultSerializationOptions)
            : new JsonSerializerOptions(serializerOptions);
    }
}
