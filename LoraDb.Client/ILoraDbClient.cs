namespace LoraDb.Client;

public interface ILoraDbClient : IAsyncDisposable
{
    Task<LoraDbQueryResult> ExecuteAsync(
        string query,
        IReadOnlyDictionary<string, object?>? parameters = null,
        string format = Models.LoraDbQueryRequest.DefaultFormat,
        CancellationToken cancellationToken = default);
}
