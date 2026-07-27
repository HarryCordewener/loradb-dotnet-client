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

Use embedded mode to execute queries through the native `lora_ffi` bridge.

Embedded mode needs the `lora_ffi` native library at runtime; `LoraDb.Client`
does not ship it. Add the companion package:

```bash
dotnet add package LoraDb.Client.Native
```

```csharp
using LoraDb.Client;

await using var client = LoraDbClient.CreateEmbedded();
```

> `LoraDb.Client.Native` bundles BUSL-1.1 licensed binaries — see the repository
> `LICENSE` and the `THIRD-PARTY-NOTICES.md` shipped in that package.
> Embedded mode is not supported on `netstandard2.1`.
> Embedded mode currently supports only `rows` query format.

Persistent embedded mode (named database):

```csharp
await using var client = LoraDbClient.CreateEmbedded(new LoraDbEmbeddedOpenOptions
{
    DatabaseName = "app",
    DatabaseDirectory = "/var/lib/loradb"
});
```

---

## 2) Execute queries

```csharp
UserNameRow? first = null;
await foreach (var row in client.ExecuteRowsAsync<UserNameRow>(
                   "MATCH (u:User) WHERE u.name = $name RETURN u.name AS name",
                   new Dictionary<string, object?> { ["name"] = "Alice" }))
{
    first = row;
    break;
}

var name = first?.Name;

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

`ExecuteRowsAsync` streams `IAsyncEnumerable<T>` for `await foreach` consumption.

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

By default, embedded mode loads library name `lora_ffi`. `LoraDb.Client`
registers a resolver that looks for the RID-specific binary under
`runtimes/{rid}/native/`, first next to the loaded assembly and then under
`AppContext.BaseDirectory` — that is where the `LoraDb.Client.Native` package
places the binaries. If neither is found the OS default search is used, and a
missing library surfaces as `DllNotFoundException`.

If needed, set a custom name (or an absolute path to your own build) via
options:

```csharp
services.AddLoraDb(new LoraDbClientOptions
{
    Mode = LoraDbClientMode.Embedded,
    NativeLibraryName = "lora_ffi"
});
```

Required exported symbols in native library:

- `lora_db_new`
- `lora_db_new_named`
- `lora_db_new_with_wal`
- `lora_db_free`
- `lora_db_execute_json`
- `lora_db_explain_json`
- `lora_db_profile_json`
- `lora_db_save_snapshot`
- `lora_db_load_snapshot`
- `lora_string_free`

Embedded management APIs are available via `LoraDbEmbeddedManagementClient`:

```csharp
await using var embedded = LoraDbEmbeddedManagementClient.Create(new LoraDbEmbeddedOpenOptions
{
    DatabaseName = "app",
    DatabaseDirectory = "/var/lib/loradb"
});

var plan = await embedded.ExplainAsync("MATCH (n) RETURN n");
var profile = await embedded.ProfileAsync("MATCH (n) RETURN n");
var snapshot = await embedded.SaveSnapshotAsync("/var/lib/loradb/snapshots/app.bin");
await embedded.LoadSnapshotAsync(snapshot.Path);
```

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
await foreach (var person in client.FindNodesAsync<PersonNode>("Person",
                   new Dictionary<string, object?> { ["name"] = "Alice" }))
{
    // process person
}

// MATCH (n:Person {id: $filter_id}) RETURN n LIMIT 1
var one = await client.FindNodeAsync<PersonNode>("Person",
    new Dictionary<string, object?> { ["id"] = 42 });
// Returns null when no match exists.
```

### Update

```csharp
// MATCH (n:Person {id: $match_id}) SET n.age = $set_age RETURN n
await foreach (var updated in client.UpdateNodesAsync<PersonNode>("Person",
                   match: new Dictionary<string, object?> { ["id"] = 42 },
                   properties: new Dictionary<string, object?> { ["age"] = 31 }))
{
    // process updated node
}
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

## 9) HTTP management client

`LoraDbHttpManagementClient` extends the standard query API with HTTP-specific
management operations: health check, query explain/profile, and the opt-in
admin snapshot/WAL endpoints.

### Create

```csharp
await using var mgmt = LoraDbHttpManagementClient.Create(new Uri("http://127.0.0.1:4747/"));
// or with IHttpClientFactory:
await using var mgmt = LoraDbHttpManagementClient.Create(endpoint, httpClientFactory);
```

`ILoraDbHttpManagementClient` extends `ILoraDbClient`, so it also supports
`ExecuteAsync`, CRUD extensions, batches, and typed streaming.

### Health check

```csharp
var health = await mgmt.HealthAsync();
Console.WriteLine(health.Status);   // "ok"
Console.WriteLine(health.IsHealthy); // true
```

### Explain (compile without executing)

```csharp
var plan = await mgmt.ExplainAsync(
    "MATCH (p:Person) WHERE p.name = $name RETURN p",
    new Dictionary<string, object?> { ["name"] = "Alice" });

Console.WriteLine(plan.Shape);          // "readOnly"
Console.WriteLine(plan.IsReadOnly);     // true
Console.WriteLine(plan.Tree.Operator);  // e.g. "Projection"
```

### Profile (execute with metrics)

```csharp
var profile = await mgmt.ProfileAsync("MATCH (p:Person) RETURN p");

Console.WriteLine(profile.Metrics.TotalElapsedNs);
Console.WriteLine(profile.Metrics.TotalRows);
foreach (var (operatorId, metrics) in profile.Metrics.PerOperator)
    Console.WriteLine($"  [{operatorId}] rows={metrics.Rows} elapsedNs={metrics.ElapsedNs}");
```

`ProfileAsync` runs the query for real — mutations have the same side-effects
as `ExecuteAsync`.

### Snapshot save / load (opt-in: requires `--snapshot-path` on the server)

```csharp
// Save to the server-configured default path
var meta = await mgmt.SaveSnapshotAsync();
Console.WriteLine($"Saved {meta.NodeCount} nodes to {meta.Path}");

// Save to an explicit path (overrides server default for this request)
var meta = await mgmt.SaveSnapshotAsync("/var/backups/lora/2026-04-24.bin");

// Restore
var meta = await mgmt.LoadSnapshotAsync();
```

### Checkpoint (opt-in: requires `--wal-dir` on the server)

```csharp
var meta = await mgmt.CheckpointAsync();
Console.WriteLine($"Checkpoint LSN: {meta.WalLsn}");
```

### WAL status and truncation (opt-in: requires `--wal-dir` on the server)

```csharp
var status = await mgmt.WalStatusAsync();
Console.WriteLine($"Durable LSN: {status.DurableLsn}, next: {status.NextLsn}");
if (status.BgFailure is not null)
    Console.Error.WriteLine($"WAL background failure: {status.BgFailure}");

// Truncate up to the current durableLsn
await mgmt.TruncateWalAsync();

// Truncate up to a specific LSN
await mgmt.TruncateWalAsync(fenceLsn: status.DurableLsn);
```

Admin endpoints return `HttpRequestException` with HTTP 404 when the
corresponding server flag was not set at start-up.

---

## 10) Testing with real LoraDB

To run integration tests in this repository:

- `LORADB_RUN_INTEGRATION_TESTS=1`
- `LORADB_FFI_LIBRARY_PATH=<absolute-path-to-lora_ffi-binary>`
- `LORADB_HTTP_IMAGE=<optional-image>`

```bash
dotnet test LoraDb.Client.IntegrationTests/LoraDb.Client.IntegrationTests.csproj
```
