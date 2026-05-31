using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using LoraDb.Client.Serialization;

namespace LoraDb.Client;

public sealed class LoraDbQueryResult : IDisposable
{
    private readonly JsonDocument _document;
    private readonly JsonSerializerOptions _serializerOptions;

    internal LoraDbQueryResult(JsonDocument document, JsonSerializerOptions? serializerOptions = null)
    {
        _document = document;
        _serializerOptions = LoraDbJsonSerializerOptions.CreateResultOptions(serializerOptions);
    }

    public JsonElement Root => _document.RootElement;

    public T Deserialize<T>() => DeserializeElement<T>(Root, _serializerOptions, "root");

    public T Deserialize<T>(JsonSerializerOptions serializerOptions)
    {
        if (serializerOptions is null)
            throw new ArgumentNullException(nameof(serializerOptions));

        return DeserializeElement<T>(Root, serializerOptions, "root");
    }

    public IReadOnlyList<T> ReadRows<T>()
    {
        var rows = DeserializeElement<List<T>>(GetRequiredProperty("rows"), _serializerOptions, "rows");
        return rows;
    }

    public LoraDbRowsResult<T> ReadRowsEnvelope<T>()
        => new(ReadRows<T>());

    public LoraDbRowArraysResult<TValue> ReadRowArrays<TValue>()
    {
        var columns = DeserializeElement<List<string>>(GetRequiredProperty("columns"), _serializerOptions, "columns");
        var rows = DeserializeElement<List<List<TValue>>>(GetRequiredProperty("rows"), _serializerOptions, "rows");
        return new(columns, rows.ConvertAll(static row => (IReadOnlyList<TValue>)row));
    }

    public LoraDbGraphResult<TNode, TRelationship> ReadGraph<TNode, TRelationship>()
    {
        var graph = GetRequiredProperty("graph");
        return new(new LoraDbGraphPayload<TNode, TRelationship>(
            DeserializeElement<List<TNode>>(GetRequiredProperty(graph, "nodes", "graph"), _serializerOptions, "graph.nodes"),
            DeserializeElement<List<TRelationship>>(GetRequiredProperty(graph, "relationships", "graph"), _serializerOptions, "graph.relationships")));
    }

    public LoraDbCombinedResult<TData, TNode, TRelationship> ReadCombined<TData, TNode, TRelationship>()
    {
        var columns = DeserializeElement<List<string>>(GetRequiredProperty("columns"), _serializerOptions, "columns");
        var data = DeserializeElement<List<TData>>(GetRequiredProperty("data"), _serializerOptions, "data");
        var graph = ReadGraph<TNode, TRelationship>().Graph;
        return new(columns, data, graph);
    }

    public IReadOnlyList<T> ReadRows<T>(JsonTypeInfo<T> typeInfo)
        => ReadArray(GetRequiredProperty("rows"), typeInfo, "rows");

    public LoraDbRowsResult<T> ReadRowsEnvelope<T>(JsonTypeInfo<T> typeInfo)
        => new(ReadRows(typeInfo));

    public LoraDbRowArraysResult<TValue> ReadRowArrays<TValue>(JsonTypeInfo<TValue> typeInfo)
    {
        var columns = DeserializeElement<List<string>>(GetRequiredProperty("columns"), _serializerOptions, "columns");
        var rowArraysElement = GetRequiredProperty("rows");
        if (rowArraysElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Result payload 'rows' is not an array.");

        var rows = new List<IReadOnlyList<TValue>>(rowArraysElement.GetArrayLength());
        foreach (var rowElement in rowArraysElement.EnumerateArray())
        {
            rows.Add(ReadArray(rowElement, typeInfo, "rows[*]"));
        }

        return new(columns, rows);
    }

    public LoraDbGraphResult<TNode, TRelationship> ReadGraph<TNode, TRelationship>(
        JsonTypeInfo<TNode> nodeTypeInfo,
        JsonTypeInfo<TRelationship> relationshipTypeInfo)
    {
        var graph = GetRequiredProperty("graph");
        return new(new LoraDbGraphPayload<TNode, TRelationship>(
            ReadArray(GetRequiredProperty(graph, "nodes", "graph"), nodeTypeInfo, "graph.nodes"),
            ReadArray(GetRequiredProperty(graph, "relationships", "graph"), relationshipTypeInfo, "graph.relationships")));
    }

    public LoraDbCombinedResult<TData, TNode, TRelationship> ReadCombined<TData, TNode, TRelationship>(
        JsonTypeInfo<TData> dataTypeInfo,
        JsonTypeInfo<TNode> nodeTypeInfo,
        JsonTypeInfo<TRelationship> relationshipTypeInfo)
    {
        var columns = DeserializeElement<List<string>>(GetRequiredProperty("columns"), _serializerOptions, "columns");
        var data = ReadArray(GetRequiredProperty("data"), dataTypeInfo, "data");
        var graph = ReadGraph(nodeTypeInfo, relationshipTypeInfo).Graph;
        return new(columns, data, graph);
    }

    public void Dispose() => _document.Dispose();

    private static T DeserializeElement<T>(JsonElement element, JsonSerializerOptions serializerOptions, string context)
    {
        try
        {
            var value = element.Deserialize<T>(serializerOptions);
            if (value is null)
                throw new InvalidOperationException($"Result payload '{context}' deserialized to null.");

            return value;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException($"Result payload '{context}' could not be deserialized to {typeof(T).Name}.", ex);
        }
    }

    private static IReadOnlyList<T> ReadArray<T>(JsonElement element, JsonTypeInfo<T> typeInfo, string context)
    {
        if (typeInfo is null)
            throw new ArgumentNullException(nameof(typeInfo));
        if (element.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Result payload '{context}' is not an array.");

        var values = new List<T>(element.GetArrayLength());
        foreach (var item in element.EnumerateArray())
        {
            T? value;
            try
            {
                value = item.Deserialize(typeInfo);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                throw new InvalidOperationException($"Result payload '{context}' could not be deserialized to {typeof(T).Name}.", ex);
            }

            // Null JSON elements are preserved for reference types and Nullable<T>.
            // Non-nullable value types will have thrown a JsonException above when the database
            // returns a null cell, which is re-thrown as InvalidOperationException.
            values.Add(value!);
        }

        return values;
    }

    private JsonElement GetRequiredProperty(string propertyName)
        => GetRequiredProperty(Root, propertyName, "root");

    private static JsonElement GetRequiredProperty(JsonElement parent, string propertyName, string context)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
            throw new InvalidOperationException($"Result payload '{context}' does not contain required property '{propertyName}'.");

        return value;
    }
}
