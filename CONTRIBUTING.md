# Contributing to loradb-client

Thank you for considering contributing! Please follow these guidelines.

## Getting started

1. Fork the repository and clone your fork.
2. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later.
3. Run `dotnet restore LoraDb.Client.slnx` to restore packages.
4. Run `dotnet build LoraDb.Client.slnx` to verify the build.
5. Run `dotnet test LoraDb.Client.slnx` to run the test suite.

## Integration tests

Integration tests exercise real HTTP and Embedded modes.

- Set `LORADB_RUN_INTEGRATION_TESTS=1`.
- Optionally set `LORADB_HTTP_IMAGE` to override the default LoraDB server container image (`ghcr.io/lora-db/lora-server:latest`) for HTTP mode testing.
- Set `LORADB_FFI_LIBRARY_PATH` to a real `lora_ffi` binary path for Embedded mode testing.

Run them with:

```bash
dotnet test LoraDb.Client.IntegrationTests/LoraDb.Client.IntegrationTests.csproj
```

To run HTTP integration tests in GitHub Actions, optionally set this repository variable to override the default (`ghcr.io/lora-db/lora-server:latest`):

- `LORADB_HTTP_IMAGE=<loradb-server-image>`

## Commit conventions

We use [Conventional Commits](https://www.conventionalcommits.org/) so that
[git-cliff](https://git-cliff.org/) can generate the changelog automatically.

| Prefix | When to use |
|--------|-------------|
| `feat:` | A new feature |
| `fix:` | A bug fix |
| `docs:` | Documentation-only changes |
| `refactor:` | A code change that neither fixes a bug nor adds a feature |
| `test:` | Adding or correcting tests |
| `chore:` / `ci:` / `build:` | Tooling, CI, or build changes |
| `perf:` | Performance improvements |
| `revert:` | Reverts a prior commit |

Include a scope in parentheses when useful, e.g. `feat(http): add retry policy`.

## Pull requests

- Keep PRs focused on one change.
- Make sure all tests pass before opening a PR.
- Add or update tests for any behaviour you change.
- Update `CHANGELOG.md` under `## [Unreleased]` with a summary if appropriate.

## Code style

The repository ships with an `.editorconfig`. Most IDEs will pick this up
automatically. Avoid reformatting unrelated code in a PR.
