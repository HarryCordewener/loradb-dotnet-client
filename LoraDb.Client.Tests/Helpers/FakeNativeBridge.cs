using LoraDb.Client.Native;

namespace LoraDb.Client.Tests.Helpers;

internal sealed class FakeNativeBridge : ILoraDbNativeBridge
{
    private readonly string _responseJson;

    public string? LastRequestJson { get; private set; }
    public int CallCount { get; private set; }

    public FakeNativeBridge(string responseJson = """{"rows":[{"name":"Alice"}]}""")
    {
        _responseJson = responseJson;
    }

    public string ExecuteJson(string requestJson)
    {
        LastRequestJson = requestJson;
        CallCount++;
        return _responseJson;
    }

    public void Dispose() { }
}
