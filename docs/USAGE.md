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

## 7) Testing with real LoraDB

To run integration tests in this repository:

- `LORADB_RUN_INTEGRATION_TESTS=1`
- `LORADB_FFI_LIBRARY_PATH=<absolute-path-to-lora_ffi-binary>`
- `LORADB_HTTP_IMAGE=<optional-image>`

```bash
dotnet test LoraDb.Client.IntegrationTests/LoraDb.Client.IntegrationTests.csproj
```
