# Contributing to loradb-client

Thank you for considering contributing! Please follow these guidelines.

## Getting started

1. Fork the repository and clone your fork.
2. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later.
3. Run `dotnet restore LoraDb.Client.slnx` to restore packages.
4. Run `dotnet build LoraDb.Client.slnx` to verify the build.
5. Run `dotnet run --project LoraDb.Client.Tests/LoraDb.Client.Tests.csproj` to run the test suite.

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
