# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Features

- Initial release of `LoraDb.Client` — a dual-mode .NET 8 client library for
  [LoraDB](https://github.com/lora-db/lora), supporting both HTTP transport and
  embedded Rust FFI (P/Invoke) modes.
- Updated target framework support to .NET 10 and added a .NET Standard 2.1 target for `LoraDb.Client`.
- `LoraDbClient.CreateHttp` and `LoraDbClient.CreateEmbedded` factory methods.
- `ILoraDbClient` interface for dependency-injection scenarios.
- `ServiceCollectionExtensions.AddLoraDb` with action, connection-string, and
  options-object overloads.
- `LoraDbClientOptions` with `FromConnectionString` parser.
- Full TUnit 1.x test suite covering Cypher CREATE, MATCH, UPDATE/DELETE, error
  handling, result formats, and DI registration.
- `LoraDbClientCrudExtensions` — structured CRUD helpers (`CreateNodeAsync`,
  `FindNodesAsync`, `FindNodeAsync`, `UpdateNodesAsync`, `DeleteNodesAsync`,
  `MergeNodeAsync`) that build Cypher queries automatically from label and
  property-map inputs.
- `LoraDbBatch` / `LoraDbBatchResult` — sequential fail-fast batch executor that
  runs multiple statements one-after-another using LoraDB's auto-commit
  semantics; accessible via `ILoraDbClient.CreateBatch()`.
- `LoraDbHttpManagementClient` / `ILoraDbHttpManagementClient` — HTTP management
  client exposing `HealthAsync`, `ExplainAsync` (compile-only plan),
  `ProfileAsync` (execute with runtime metrics), `SaveSnapshotAsync`,
  `LoadSnapshotAsync`, `CheckpointAsync`, `WalStatusAsync`, and
  `TruncateWalAsync`; backed by five new model types (`LoraDbHealthResult`,
  `LoraDbQueryPlan`, `LoraDbQueryProfile`, `LoraDbSnapshotMeta`,
  `LoraDbWalStatus`).
