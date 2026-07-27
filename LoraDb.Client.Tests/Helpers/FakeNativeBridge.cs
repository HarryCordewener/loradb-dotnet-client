using LoraDb.Client.Models;
using LoraDb.Client.Native;

namespace LoraDb.Client.Tests.Helpers;

internal sealed class FakeNativeBridge : ILoraDbNativeBridge
{
    private readonly Func<string, string>? _responseFactory;
    private readonly string _responseJson;

    public string? LastRequestJson { get; private set; }
    public int CallCount { get; private set; }

    public FakeNativeBridge(string responseJson = """{"rows":[{"name":"Alice"}]}""")
    {
        _responseJson = responseJson;
    }

    /// <summary>
    /// Creates a bridge where each call is handled by <paramref name="responseFactory"/>.
    /// The factory receives the raw request JSON and returns the response JSON string,
    /// or may throw to simulate a database error.
    /// </summary>
    public FakeNativeBridge(Func<string, string> responseFactory)
    {
        _responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
        _responseJson = string.Empty;
    }

    public string ExecuteJson(string requestJson)
    {
        LastRequestJson = requestJson;
        CallCount++;
        return _responseFactory is not null ? _responseFactory(requestJson) : _responseJson;
    }

    public string ExplainJson(string requestJson) => ExecuteJson(requestJson);

    public string ProfileJson(string requestJson) => ExecuteJson(requestJson);

    public LoraDbSnapshotMeta SaveSnapshot(string path) => new()
    {
        FormatVersion = 1,
        NodeCount = 0,
        RelationshipCount = 0,
        WalLsn = null,
        Path = path
    };

    public LoraDbSnapshotMeta LoadSnapshot(string path) => new()
    {
        FormatVersion = 1,
        NodeCount = 0,
        RelationshipCount = 0,
        WalLsn = null,
        Path = path
    };

    public void Dispose() { }
}
