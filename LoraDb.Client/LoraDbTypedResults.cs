namespace LoraDb.Client;

public sealed class LoraDbRowsResult<T>
{
    public LoraDbRowsResult(IReadOnlyList<T> rows)
    {
        Rows = rows ?? throw new ArgumentNullException(nameof(rows));
    }

    public IReadOnlyList<T> Rows { get; }
}

public sealed class LoraDbRowArraysResult<T>
{
    public LoraDbRowArraysResult(IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<T>> rows)
    {
        Columns = columns ?? throw new ArgumentNullException(nameof(columns));
        Rows = rows ?? throw new ArgumentNullException(nameof(rows));
    }

    public IReadOnlyList<string> Columns { get; }

    public IReadOnlyList<IReadOnlyList<T>> Rows { get; }
}

public sealed class LoraDbGraphPayload<TNode, TRelationship>
{
    public LoraDbGraphPayload(IReadOnlyList<TNode> nodes, IReadOnlyList<TRelationship> relationships)
    {
        Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        Relationships = relationships ?? throw new ArgumentNullException(nameof(relationships));
    }

    public IReadOnlyList<TNode> Nodes { get; }

    public IReadOnlyList<TRelationship> Relationships { get; }
}

public sealed class LoraDbGraphResult<TNode, TRelationship>
{
    public LoraDbGraphResult(LoraDbGraphPayload<TNode, TRelationship> graph)
    {
        Graph = graph ?? throw new ArgumentNullException(nameof(graph));
    }

    public LoraDbGraphPayload<TNode, TRelationship> Graph { get; }
}

public sealed class LoraDbCombinedResult<TData, TNode, TRelationship>
{
    public LoraDbCombinedResult(
        IReadOnlyList<string> columns,
        IReadOnlyList<TData> data,
        LoraDbGraphPayload<TNode, TRelationship> graph)
    {
        Columns = columns ?? throw new ArgumentNullException(nameof(columns));
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Graph = graph ?? throw new ArgumentNullException(nameof(graph));
    }

    public IReadOnlyList<string> Columns { get; }

    public IReadOnlyList<TData> Data { get; }

    public LoraDbGraphPayload<TNode, TRelationship> Graph { get; }
}
