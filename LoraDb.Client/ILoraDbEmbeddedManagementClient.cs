using LoraDb.Client.Models;

namespace LoraDb.Client;

/// <summary>
/// Embedded-mode management APIs backed by native <c>lora_ffi</c>.
/// </summary>
public interface ILoraDbEmbeddedManagementClient : ILoraDbClient, ILoraDbCapabilitiesProvider
{
    Task<LoraDbQueryPlan> ExplainAsync(
        string query,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default);

    Task<LoraDbQueryProfile> ProfileAsync(
        string query,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default);

    Task<LoraDbSnapshotMeta> SaveSnapshotAsync(
        string path,
        CancellationToken cancellationToken = default);

    Task<LoraDbSnapshotMeta> LoadSnapshotAsync(
        string path,
        CancellationToken cancellationToken = default);
}
