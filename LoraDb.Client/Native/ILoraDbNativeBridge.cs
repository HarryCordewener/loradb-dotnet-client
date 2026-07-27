using LoraDb.Client.Models;

namespace LoraDb.Client.Native;

public interface ILoraDbNativeBridge : IDisposable
{
    string ExecuteJson(string requestJson);

    string ExplainJson(string requestJson);

    string ProfileJson(string requestJson);

    LoraDbSnapshotMeta SaveSnapshot(string path);

    LoraDbSnapshotMeta LoadSnapshot(string path);
}
