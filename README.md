# LoraDb.Client

A modern .NET client for [LoraDB](https://loradb.com), supporting:

- **HTTP mode** against `lora-server` (`POST /query`)
- **Embedded mode** via P/Invoke with `lora_ffi`

## Why use this client?

- Async-first API (`ExecuteAsync`)
- Parameterized queries
- Pluggable runtime mode (HTTP or embedded)
- DI integration via `Microsoft.Extensions.DependencyInjection`
- Targets **net10.0** and **netstandard2.1**

## Installation

```bash
dotnet add package LoraDb.Client
```

## Quick start

```csharp
using LoraDb.Client;

await using var client = LoraDbClient.CreateHttp(new Uri("http://127.0.0.1:4747/"));

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

Need low-level JSON access? `ExecuteAsync` still returns `LoraDbQueryResult` with `Root`.

## Usage documentation

For a practical, summarized guide with HTTP mode, embedded mode, DI setup, result handling, and troubleshooting, see:

- [docs/USAGE.md](docs/USAGE.md)

## Runtime modes

### HTTP mode

```csharp
await using var client = LoraDbClient.CreateHttp(new Uri("http://127.0.0.1:4747/"));
```

### Embedded mode

```csharp
await using var client = LoraDbClient.CreateEmbedded();
```

Persistent embedded databases:

```csharp
await using var client = LoraDbClient.CreateEmbedded(new LoraDbEmbeddedOpenOptions
{
    DatabaseName = "app",
    DatabaseDirectory = "/var/lib/loradb",
});
```

Embedded mode uses `lora_ffi` and expects these exported symbols:

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

## Development

```bash
dotnet restore LoraDb.Client.slnx
dotnet build LoraDb.Client.slnx
dotnet test LoraDb.Client.slnx
```

### Integration tests

Integration tests exercise real HTTP and embedded modes.

Required environment variables:

- `LORADB_RUN_INTEGRATION_TESTS=1`
- `LORADB_FFI_LIBRARY_PATH=<absolute-path-to-lora_ffi-binary>`

Optional:

- `LORADB_HTTP_IMAGE=<loradb-server-image>` (defaults to `ghcr.io/lora-db/lora-server:latest`)

Run integration tests:

```bash
dotnet test LoraDb.Client.IntegrationTests/LoraDb.Client.IntegrationTests.csproj
```

### Build/pin native `lora_ffi`

The pinned upstream version is tracked in `LoraDb.Client.Native/lora-ffi.version`.

```bash
# Build using the currently pinned upstream ref
./scripts/build-lora-ffi.sh

# Update to a new upstream ref and persist the pin
./scripts/build-lora-ffi.sh --ref <tag-or-commit> --update-pin
```
