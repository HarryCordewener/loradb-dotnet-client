# LoraDb.Client

A modern .NET client library for [LoraDB](https://loradb.com) with two runtime modes:

- **HTTP mode** against `lora-server` (`POST /query`)
- **Embedded mode** via **P/Invoke** into a Rust FFI library

## Install

```bash
dotnet add package LoraDb.Client
```

## HTTP mode

```csharp
await using var client = LoraDbClient.CreateHttp(new Uri("http://127.0.0.1:4747/"));
using var result = await client.ExecuteAsync(
    "MATCH (u:User) WHERE u.name = $name RETURN u.name AS name",
    new Dictionary<string, object?> { ["name"] = "Alice" });
```

## Embedded mode (Rust FFI)

By default this expects a native library named `lora_ffi` with the exported symbols:

- `lora_execute_json`
- `lora_string_free`

The `LoraDb.Client` NuGet package ships RID-specific native assets under `runtimes/{rid}/native/` for common desktop/server platforms.

```csharp
await using var client = LoraDbClient.CreateEmbedded();
using var result = await client.ExecuteAsync("MATCH (u:User) RETURN u");
```

## Development

```bash
dotnet restore LoraDb.Client.slnx
dotnet build LoraDb.Client.slnx
dotnet test LoraDb.Client.slnx
```

### Integration tests

Integration tests exercise real HTTP and Embedded modes:

- `LORADB_RUN_INTEGRATION_TESTS=1`
- `LORADB_HTTP_IMAGE=<loradb-server-image>`
- `LORADB_FFI_LIBRARY_PATH=<absolute-path-to-lora_ffi-binary>`

```bash
dotnet test LoraDb.Client.IntegrationTests/LoraDb.Client.IntegrationTests.csproj
```
