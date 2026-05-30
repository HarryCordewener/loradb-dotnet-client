namespace LoraDb.Client;

public sealed record LoraDbRowsResult<T>(IReadOnlyList<T> Rows);

public sealed record LoraDbRowArraysResult<T>(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<T>> Rows);

public sealed record LoraDbGraphPayload<TNode, TRelationship>(
    IReadOnlyList<TNode> Nodes,
    IReadOnlyList<TRelationship> Relationships);

public sealed record LoraDbGraphResult<TNode, TRelationship>(LoraDbGraphPayload<TNode, TRelationship> Graph);

public sealed record LoraDbCombinedResult<TData, TNode, TRelationship>(
    IReadOnlyList<string> Columns,
    IReadOnlyList<TData> Data,
    LoraDbGraphPayload<TNode, TRelationship> Graph);
