using System.Text.Json;

namespace LoraDb.Client;

public sealed class LoraDbQueryResult : IDisposable
{
    private readonly JsonDocument _document;

    internal LoraDbQueryResult(JsonDocument document)
    {
        _document = document;
    }

    public JsonElement Root => _document.RootElement;

    public void Dispose() => _document.Dispose();
}
