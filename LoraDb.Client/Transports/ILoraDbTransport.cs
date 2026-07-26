namespace LoraDb.Client.Transports;

public interface ILoraDbTransport : IAsyncDisposable
{
    LoraDbClientCapabilities Capabilities { get; }

    Task<LoraDbQueryResult> ExecuteAsync(
        string query,
        IReadOnlyDictionary<string, object?>? parameters,
        string format,
        CancellationToken cancellationToken);
}
