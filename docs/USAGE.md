# LoraDb.Client usage guide

This guide summarizes how to use `LoraDb.Client` in production-style .NET apps.

## 1) Create a client

### HTTP mode

Use HTTP mode when you connect to a running `lora-server` instance.

```csharp
using LoraDb.Client;

await using var client = LoraDbClient.CreateHttp(new Uri("http://127.0.0.1:4747/"));
```

You can also create HTTP clients using `IHttpClientFactory`:

```csharp
await using var client = LoraDbClient.CreateHttp(endpoint, httpClientFactory);
```

### Embedded mode

Use embedded mode to execute queries through the native `lora_ffi` bridge:

```csharp
using LoraDb.Client;

await using var client = LoraDbClient.CreateEmbedded();
```

> Embedded mode is not supported on `netstandard2.1`.

---

## 2) Execute queries

```csharp
var rows = await client.ExecuteRowsAsync<UserNameRow>(
    "MATCH (u:User) WHERE u.name = $name RETURN u.name AS name",
    new Dictionary<string, object?> { ["name"] = "Alice" });

var name = rows[0].Name;

public sealed class UserNameRow
{
    public string Name { get; init; } = string.Empty;
}
```

- `query`: required Cypher query string
- `parameters`: optional map passed as `$paramName`
- `format`: optional response format (default: `"rows"`)

Avoid passing null/whitespace queries; they throw `ArgumentException`.

---

## 3) Read results

For most use cases, use typed readers:

```csharp
using var result = await client.ExecuteAsync("MATCH (u:User) RETURN u.name AS name");
var typedRows = result.ReadRows<UserNameRow>();
```

Typed format readers:

- `ReadRows<T>()` and `ReadRowsEnvelope<T>()`
- `ReadRowArrays<T>()`
- `ReadGraph<TNode, TRelationship>()`
- `ReadCombined<TData, TNode, TRelationship>()`

`ExecuteAsync` still returns `LoraDbQueryResult` with `Root` for low-level/manual JSON access.

For AOT/source-generated serialization, use overloads that accept `JsonTypeInfo<T>`, for example:

- `result.ReadRows(MyJsonContext.Default.UserNameRow)`
- `client.ExecuteRowsAsync(query, MyJsonContext.Default.UserNameRow)`

You can provide custom `JsonSerializerOptions` when creating clients:

- `LoraDbClient.CreateHttp(endpoint, httpClient, serializerOptions: options)`
- `LoraDbClient.CreateEmbedded(serializerOptions: options)`
- `services.AddLoraDb(options => options.SerializerOptions = customOptions)`

---

## 4) Dependency injection setup

If you use `Microsoft.Extensions.DependencyInjection`, register the client once and inject `ILoraDbClient`.

### Configure with options delegate

```csharp
using LoraDb.Client.Extensions;

services.AddLoraDb(options =>
{
    options.Mode = LoraDbClientMode.Http;
    options.Endpoint = new Uri("http://127.0.0.1:4747/");
});
```

### Configure with connection string

```csharp
services.AddLoraDb("Server=http://127.0.0.1:4747/;Mode=Http");
```

Supported keys:

- `Server` / `Endpoint`
- `Mode` (`Http` or `Embedded`)
- `NativeLibrary`

---

## 5) Embedded native library notes

By default, embedded mode loads library name `lora_ffi`.

If needed, set a custom name via options:

```csharp
services.AddLoraDb(new LoraDbClientOptions
{
    Mode = LoraDbClientMode.Embedded,
    NativeLibraryName = "lora_ffi"
});
```

Required exported symbols in native library:

- `lora_db_new`
- `lora_db_free`
- `lora_db_execute_json`
- `lora_string_free`

---

## 6) Error handling and disposal

- Always `await using` the client (`IAsyncDisposable`).
- Always `using` query results to dispose the underlying `JsonDocument`.
- HTTP mode throws on non-success HTTP responses.
- Embedded mode throws `InvalidOperationException` on native execution failures.
- Typed readers throw `InvalidOperationException` when the payload shape does not match the selected format reader.

---

## 7) CRUD helper extensions

`LoraDbClientCrudExtensions` provides structured helpers that build common Cypher
queries automatically.  All methods accept a `label` string and a property
dictionary, and return rows deserialized as `T`.

> **Row shape note** – LoraDB wraps node data as
> `{"n":{"id":…,"labels":[…],"properties":{…}}}`.  Define your DTO with a
> property `n` (or use `[JsonPropertyName("n")]`) to map the node.

```csharp
public sealed class PersonNode
{
    [JsonPropertyName("n")]
    public NodeData N { get; init; } = null!;
}
public sealed class NodeData
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("labels")] public List<string> Labels { get; init; } = new();
    [JsonPropertyName("properties")] public JsonElement Properties { get; init; }
}
```

### Create

```csharp
// CREATE (n:Person {name: $create_name, age: $create_age}) RETURN n
var row = await client.CreateNodeAsync<PersonNode>("Person",
    new Dictionary<string, object?> { ["name"] = "Alice", ["age"] = 30 });

// CREATE (n:Tag) RETURN n  (no inline properties)
var empty = await client.CreateNodeAsync<PersonNode>("Tag");
```

### Read

```csharp
// MATCH (n:Person {name: $filter_name}) RETURN n
var all = await client.FindNodesAsync<PersonNode>("Person",
    new Dictionary<string, object?> { ["name"] = "Alice" });

// MATCH (n:Person {id: $filter_id}) RETURN n LIMIT 1
var one = await client.FindNodeAsync<PersonNode>("Person",
    new Dictionary<string, object?> { ["id"] = 42 });
// Returns null when no match exists.
```

### Update

```csharp
// MATCH (n:Person {id: $match_id}) SET n.age = $set_age RETURN n
var updated = await client.UpdateNodesAsync<PersonNode>("Person",
    match: new Dictionary<string, object?> { ["id"] = 42 },
    properties: new Dictionary<string, object?> { ["age"] = 31 });
```

### Delete

```csharp
// MATCH (n:Person {id: $match_id}) DETACH DELETE n
await client.DeleteNodesAsync("Person",
    match: new Dictionary<string, object?> { ["id"] = 42 });

// MATCH (n:TempNode) DETACH DELETE n  (no filter = all nodes with label)
await client.DeleteNodesAsync("TempNode");

// Plain DELETE (no DETACH) — node must have no relationships
await client.DeleteNodesAsync("Isolated", detach: false);
```

### Merge (upsert)

```csharp
// MERGE (n:User {email: $merge_email}) RETURN n
var row = await client.MergeNodeAsync<PersonNode>("User",
    new Dictionary<string, object?> { ["email"] = "alice@example.com" });
```

---

## 8) Batch execution

`LoraDbBatch` executes multiple Cypher statements sequentially against the same
client.  LoraDB uses auto-commit semantics, so each statement is its own
transaction.  The batch provides **fail-fast** behaviour: if one statement
throws, the remaining statements are not executed.

```csharp
using var batchResult = await client.CreateBatch()
    .Add("CREATE (:Person {name: $name})", new Dictionary<string, object?> { ["name"] = "Alice" })
    .Add("CREATE (:Person {name: $name})", new Dictionary<string, object?> { ["name"] = "Bob" })
    .Add("MATCH (n:Person) RETURN count(n) AS total")
    .ExecuteAsync();

// batchResult.Results[2] holds the count query result
var total = batchResult.Results[2].Root.GetProperty("rows")[0].GetProperty("total").GetInt32();
```

- `LoraDbBatchResult` is `IDisposable` and disposes all contained
  `LoraDbQueryResult` instances when you dispose it.
- Use `using var` or call `Dispose()` explicitly.

---

## 9) Testing with real LoraDB

To run integration tests in this repository:

- `LORADB_RUN_INTEGRATION_TESTS=1`
- `LORADB_FFI_LIBRARY_PATH=<absolute-path-to-lora_ffi-binary>`
- `LORADB_HTTP_IMAGE=<optional-image>`

```bash
dotnet test LoraDb.Client.IntegrationTests/LoraDb.Client.IntegrationTests.csproj
```
