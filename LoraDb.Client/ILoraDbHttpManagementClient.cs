using LoraDb.Client.Models;

namespace LoraDb.Client;

/// <summary>
/// Extends <see cref="ILoraDbClient"/> with HTTP-specific management operations
/// available on a running <c>lora-server</c> instance.
/// </summary>
/// <remarks>
/// <para>
/// Admin endpoints (<see cref="SaveSnapshotAsync"/>, <see cref="LoadSnapshotAsync"/>,
/// <see cref="CheckpointAsync"/>, <see cref="WalStatusAsync"/>,
/// <see cref="TruncateWalAsync"/>) are opt-in on the server side and will throw
/// <see cref="System.Net.Http.HttpRequestException"/> with HTTP 404 when the
/// corresponding server flag (<c>--snapshot-path</c> or <c>--wal-dir</c>) was not
/// set at server start-up.
/// </para>
/// <para>
/// Use <see cref="LoraDbHttpManagementClient.Create(Uri, System.Net.Http.HttpClient?, System.Text.Json.JsonSerializerOptions?)"/>
/// to obtain an instance.
/// </para>
/// </remarks>
public interface ILoraDbHttpManagementClient : ILoraDbClient
{
    /// <summary>
    /// Calls <c>GET /health</c> and returns the server liveness result.
    /// </summary>
    Task<LoraDbHealthResult> HealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /explain</c> to compile <paramref name="query"/> and return
    /// its query plan.  The executor is never invoked — mutating queries leave the
    /// graph untouched.
    /// </summary>
    /// <param name="query">Cypher query to compile.</param>
    /// <param name="parameters">Bound parameters used when planning expressions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<LoraDbQueryPlan> ExplainAsync(
        string query,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /profile</c> to execute <paramref name="query"/> and return
    /// the plan plus runtime metrics.  The query runs for real; mutations produce
    /// the same side-effects as <see cref="ILoraDbClient.ExecuteAsync"/>.
    /// </summary>
    /// <param name="query">Cypher query to execute and profile.</param>
    /// <param name="parameters">Bound parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<LoraDbQueryProfile> ProfileAsync(
        string query,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /admin/snapshot/save</c> to save a snapshot.
    /// Requires <c>--snapshot-path</c> on the server.
    /// </summary>
    /// <param name="path">
    /// Override the server's configured snapshot path for this request only.
    /// Pass <c>null</c> to use the server-configured default.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<LoraDbSnapshotMeta> SaveSnapshotAsync(
        string? path = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /admin/snapshot/load</c> to restore a snapshot.
    /// Requires <c>--snapshot-path</c> on the server.
    /// </summary>
    /// <param name="path">
    /// Override the server's configured snapshot path for this request only.
    /// Pass <c>null</c> to use the server-configured default.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<LoraDbSnapshotMeta> LoadSnapshotAsync(
        string? path = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /admin/checkpoint</c> to write a WAL checkpoint snapshot.
    /// Requires <c>--wal-dir</c> on the server.
    /// </summary>
    /// <param name="path">
    /// Target snapshot path for the checkpoint.  Required when the server was not
    /// started with <c>--snapshot-path</c>; omit to use the server-configured
    /// default.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<LoraDbSnapshotMeta> CheckpointAsync(
        string? path = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /admin/wal/status</c> to inspect WAL state.
    /// Requires <c>--wal-dir</c> on the server.
    /// </summary>
    Task<LoraDbWalStatus> WalStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /admin/wal/truncate</c> to truncate safe WAL history.
    /// Only sealed segments are removed; the active segment and the segment
    /// immediately before it are retained.
    /// Requires <c>--wal-dir</c> on the server.
    /// </summary>
    /// <param name="fenceLsn">
    /// Truncate up to this LSN.  Pass <c>null</c> to truncate up to the current
    /// <c>durableLsn</c>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task TruncateWalAsync(
        long? fenceLsn = null,
        CancellationToken cancellationToken = default);
}
