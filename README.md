# LoraDb.Client

[![CI](https://github.com/HarryCordewener/loradb-dotnet-client/actions/workflows/ci.yml/badge.svg)](https://github.com/HarryCordewener/loradb-dotnet-client/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/LoraDb.Client?logo=nuget)](https://www.nuget.org/packages/LoraDb.Client/)
[![License: MIT + BUSL-1.1](https://img.shields.io/badge/License-MIT%20%2B%20BUSL--1.1-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0%20%7C%20netstandard2.1-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/10.0)

A modern .NET client for [LoraDB](https://github.com/lora-db/lora) — a graph database with a Cypher-like query language — supporting both **HTTP** and **embedded** (Rust FFI via P/Invoke) modes.

---

## ✨ Features

| Capability | Details |
|---|---|
| 🚀 **Async-first** | `ExecuteAsync` / `ExecuteRowsAsync<T>` returning `IAsyncEnumerable` |
| 🔒 **Parameterized queries** | Safe, strongly-typed parameter binding |
| 🔌 **Dual transport** | HTTP (`lora-server`) or embedded (`lora_ffi`) — swap at startup |
| 💉 **DI-ready** | `AddLoraDb(…)` extension for `IServiceCollection` |
| 📦 **CRUD helpers** | `CreateNodeAsync`, `FindNodesAsync`, `UpdateNodesAsync`, … |
| 🗂️ **Batch execution** | `LoraDbBatch` — sequential, fail-fast multi-statement runner |
| 🔧 **Management API** | Health, Explain, Profile, Snapshot, WAL status & truncate |
| 🎯 **Multi-target** | `net10.0` and `netstandard2.1` |

---

## 📦 Installation

```bash
dotnet add package LoraDb.Client
```

That is everything HTTP mode needs. **Embedded mode additionally requires the
native binaries**, which ship in a separate package:

```bash
dotnet add package LoraDb.Client.Native
```

`LoraDb.Client.Native` is kept separate because the `lora_ffi` binaries are
BUSL-1.1 licensed — see [License](#-license). Without it, embedded mode fails at
startup with a `DllNotFoundException`.

---

## ⚡ Quick start

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

> Need low-level JSON access? `ExecuteAsync` returns `LoraDbQueryResult` with `Root`.

---

## 🔌 Runtime modes

### HTTP mode

```csharp
await using var client = LoraDbClient.CreateHttp(new Uri("http://127.0.0.1:4747/"));
```

### Embedded mode

> Requires the `LoraDb.Client.Native` package — or your own `lora_ffi` binary,
> pointed at via `LoraDbEmbeddedOpenOptions.NativeLibraryName`.

```csharp
// In-memory (ephemeral)
await using var client = LoraDbClient.CreateEmbedded();

// Persistent on-disk
await using var client = LoraDbClient.CreateEmbedded(new LoraDbEmbeddedOpenOptions
{
    DatabaseName = "app",
    DatabaseDirectory = "/var/lib/loradb",
});
```

Embedded mode uses `lora_ffi` and expects these exported symbols:

- `lora_db_new`, `lora_db_new_named`, `lora_db_new_with_wal`
- `lora_db_free`
- `lora_db_execute_json`, `lora_db_explain_json`, `lora_db_profile_json`
- `lora_db_save_snapshot`, `lora_db_load_snapshot`
- `lora_string_free`

---

## 📖 Usage documentation

For a full guide covering HTTP mode, embedded mode, DI setup, result handling, and troubleshooting, see **[docs/USAGE.md](docs/USAGE.md)**.

---

## 🛠️ Development

```bash
dotnet restore LoraDb.Client.slnx
dotnet build LoraDb.Client.slnx
dotnet test LoraDb.Client.Tests/LoraDb.Client.Tests.csproj
```

### Integration tests

Integration tests exercise real HTTP and embedded modes and require:

| Variable | Description |
|---|---|
| `LORADB_RUN_INTEGRATION_TESTS=1` | Opt-in to run integration tests |
| `LORADB_FFI_LIBRARY_PATH` | Absolute path to `lora_ffi` binary (embedded mode) |
| `LORADB_HTTP_IMAGE` *(optional)* | Custom server image (defaults to `ghcr.io/lora-db/lora-server:latest`) |

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

---

## 🤝 Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for commit conventions, branch workflow, and PR guidelines.

---

## 🔒 Security

Please **do not** open a public issue for vulnerabilities. See [SECURITY.md](SECURITY.md) for the responsible disclosure process.

---

## 📄 License

Two licenses apply, depending on which part you use.

| Part | License |
|---|---|
| All C# source in this repo, and the managed assemblies in both packages | [MIT](LICENSE) — Copyright © 2026 Harry Cordewener |
| The `lora_ffi` native libraries (`runtimes/*/native/`), bundled only in `LoraDb.Client.Native` | [Business Source License 1.1](THIRD-PARTY-NOTICES.md) — Copyright LoraDB, Inc. |

The native libraries are compiled from [LoraDB](https://github.com/lora-db/lora),
which is licensed under BUSL-1.1 (SPDX: `BUSL-1.1`), not an open source license.
Change Date 2029-04-19, after which the Change License is Apache 2.0. The
Additional Use Grant permits internal-business and non-production use but does
not permit offering LoraDB as a database-as-a-service, hosted API, managed
database platform, or substantially similar hosted service for third parties.

**Only `LoraDb.Client.Native` bundles these binaries**, so the BUSL-1.1 terms
apply to that package. It ships `PACKAGE-LICENSE.md` (the split) and
`THIRD-PARTY-NOTICES.md` (the verbatim BUSL-1.1 text) — read them before use.
`LoraDb.Client` contains managed code only and is published as plain MIT; if you
never enable embedded mode, BUSL-1.1 does not enter your dependency graph.

> Versions 0.1.2 and earlier of `LoraDb.Client` also bundled the binaries, and
> both packages incorrectly declared `MIT` as the sole NuGet license expression.

