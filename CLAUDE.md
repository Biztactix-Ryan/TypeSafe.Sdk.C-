<!-- setup-project:start -->
# TypeSafe.Sdk
Unofficial C# SDK for the TypeSafe AI API, ported from the official Python and JavaScript SDKs (v0.6.0). Used by .NET services calling System One; started by Ryan at Biztactix.

## Stack
C# on .NET 10 (library, tests and demo all target `net10.0`; requires the .NET 10 SDK/runtime) · no database · `System.Text.Json` (JsonNode) · only NuGet dep: `Microsoft.Extensions.Logging.Abstractions` · xunit 2.9 · solution file `TypeSafe.Sdk.slnx`.

## Commands
| Task | Command |
|------|---------|
| Build | `dotnet build` |
| Test | `dotnet test` |
| Live tests | `TYPESAFE_API_KEY=... dotnet test tests/TypeSafe.Sdk.IntegrationTests` |
| Pack | `dotnet pack src/TypeSafe.Sdk/TypeSafe.Sdk.csproj -c Release -o artifacts` |
| Demo | `TYPESAFE_API_KEY=... dotnet run --project examples/TypeSafe.Sdk.Demo` |
| Version check | `scripts/check-version.sh` (optionally `scripts/check-version.sh 0.7.0`) |

## Layout
- `src/TypeSafe.Sdk/` — the library. `TypeSafeClient.cs`, `Questions.cs`, `Responses.cs`, `Exceptions.cs`, `RetryPolicy.cs`; `Internal/Transport.cs` is the retry/timeout loop.
- `tests/TypeSafe.Sdk.Tests/` — xunit, no network; `TestSupport.cs` has `StubHandler`, `CapturingLogger`, `EnvScope`.
- `examples/TypeSafe.Sdk.Demo/` — console demo mirroring the JS SDK example.
- `.project/` — ProjectMan docs, epics, stories, tasks. A worktree of the orphan `projectman` branch, gitignored on `main`; a fresh clone runs `projectman attach` to mount it.
- `artifacts/` — local pack output, gitignored.

## Deploy
Target: none (library). Distribution is source on GitHub (`Biztactix-Ryan/TypeSafe.Sdk.C-`, branch `main`); NuGet publishing and CI are TBD (tracked in ProjectMan).
Version must match in `TypeSafe.Sdk.csproj` `<Version>` and `TypeSafeConstants.Version`.
Do not push to `main` or publish a package without asking.

## Config & secrets
Runtime config is environment only: `TYPESAFE_API_KEY` (required), `TYPESAFE_BASE_URL`, `TYPESAFE_DEFAULT_MODEL`, `TYPESAFE_LOG_LEVEL`. No `.env` files; tests need no env vars. Never print the key.

## ProjectMan
Prefix TSDK. `.project/` holds epics/stories/tasks on the `projectman` branch (commit and push PM changes with `/pm commit`, never from `main`); use `/pm` for status, `/pm board` to pick work, `/pm-do <id>` to execute, `/handoff` before ending a long session. Branch names carry the task ID (`US-TSDK-n-m`) so `/commit` can reference it.

## Conventions
- Wire parity with the upstream SDKs is the rule; deliberate divergences go in `.project/DECISIONS.md` and the README "Differences" section.
- Async only, `CancellationToken` on every public call, exceptions not result codes.
- Commit style: conventional (`feat:`, `fix:`, …) with a body, from `git log`.
- `Directory.Build.props`: nullable and implicit usings on, warnings are not errors but the tree builds with zero warnings; keep it that way.
<!-- setup-project:end -->
