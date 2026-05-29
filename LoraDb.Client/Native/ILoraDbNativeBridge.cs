namespace LoraDb.Client.Native;

public interface ILoraDbNativeBridge : IDisposable
{
    string ExecuteJson(string requestJson);
}
