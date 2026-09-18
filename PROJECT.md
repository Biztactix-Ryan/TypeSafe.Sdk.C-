# TypeSafe.Sdk

Unofficial C# SDK for the TypeSafe AI API, ported from the official Python and JavaScript SDKs at v0.6.0. Repo: https://github.com/Biztactix-Ryan/TypeSafe.Sdk.C-

## Architecture

### Tech Stack

- **Language**: C# 12 (LangVersion latest on the .NET 10 SDK); retarget to C# 14 / net10.0 planned
- **Framework**: .NET class library, `TargetFramework` net8.0 (tests and demo use `RollForward=Major` because the dev machine has no .NET 8 runtime)
- **Database**: none
- **Key Libraries**: `System.Text.Json` (JsonNode-based, in-box), `Microsoft.Extensions.Logging.Abstractions` 8.0.3 (only NuGet dependency), xunit 2.9 + `Microsoft.NET.Test.Sdk` 17.12 for tests

### Components

| Path | Responsibility |
|------|----------------|
| `src/TypeSafe.Sdk/TypeSafeClient.cs` | Client, `ITypeSafeClient`, `SystemOneRequest`, option resolution (code > env > defaults) |
| `src/TypeSafe.Sdk/ModelsResource.cs` | `client.Models.ListAsync()` |
| `src/TypeSafe.Sdk/Questions.cs` | `Question` builders (`Noul`, `Choice`, `Score`, `FromJson`), validation, serialisation |
| `src/TypeSafe.Sdk/Responses.cs` | `ApiResponse`, answer types, `SystemOneResponse`, `ListModelsResponse`, `Wire` strict parser |
| `src/TypeSafe.Sdk/Exceptions.cs` | Exception hierarchy and server-message extraction |
| `src/TypeSafe.Sdk/RetryPolicy.cs` | Retry configuration record, delay calculation |
| `src/TypeSafe.Sdk/Internal/Transport.cs` | Retry loop, per-attempt timeout, header merge, logging |
| `src/TypeSafe.Sdk/Internal/*` | JSON conversion, header snapshots, Retry-After parsing, redaction, runtime info |
| `tests/TypeSafe.Sdk.Tests` | 128 xunit tests; `TestSupport.cs` has `StubHandler`, `CapturingLogger`, `EnvScope` |
| `examples/TypeSafe.Sdk.Demo` | Console demo mirroring the JS SDK's `examples/demo.ts` |

### Data Flow

Caller builds `state` + `Questions` → `SystemOneAsync` normalises questions to a `JsonObject` and merges `model` / `ExtraBody` → `Transport` merges headers, serialises once, loops attempts with timeout and backoff → response body decoded leniently → `SystemOneResponse.Parse` builds typed answers (or throws with a field path) → response carries `RequestId`, headers, and the buffered `HttpResponseMessage`.

## Key Decisions

See DECISIONS.md. Headlines: async-only API; wire-name parity for answer properties; `TotalTimeout` off by default (JS behaviour); supplied `HttpClient` not disposed unless opted in; score criteria minimum of one (Python rule, more permissive than JS's two).

## Dependencies

| Dependency | Version | Why |
|------------|---------|-----|
| Microsoft.Extensions.Logging.Abstractions | 8.0.3 | Standard .NET logging contract; ubiquitous, no transitive weight |
| xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk | 2.9.3 / 3.0.1 / 17.12.0 | Tests only |
| Upstream SDKs (reference, not a package dependency) | typesafe-sdk-python 0.6.0, @typesafe-ai/sdk 0.6.0 | Source of truth for wire behaviour |

## Development Setup

- Prerequisites: .NET SDK 10.x (9.x also works). No .NET 8 runtime is needed thanks to roll-forward.
- Build: `dotnet build` · Test: `dotnet test` · Pack: `dotnet pack src/TypeSafe.Sdk/TypeSafe.Sdk.csproj -c Release`
- Demo: `TYPESAFE_API_KEY=... dotnet run --project examples/TypeSafe.Sdk.Demo`
- Env vars (names only): `TYPESAFE_API_KEY` (required at runtime), `TYPESAFE_BASE_URL`, `TYPESAFE_DEFAULT_MODEL`, `TYPESAFE_LOG_LEVEL`.
- No seed data. Tests need no network and no env vars.

---
*Last reviewed: 2026-09-18*
