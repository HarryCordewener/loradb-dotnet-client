namespace LoraDb.Client.Transports;

public interface ILoraDbTransport : IAsyncDisposable
{
    Task<LoraDbQueryResult> ExecuteAsync(string query, IReadOnlyDictionary<string, object?>? parameters, CancellationToken cancellationToken);
}
