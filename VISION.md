# TypeSafe.Sdk — Vision

## Why this exists

TypeSafe AI ships official SDKs for Python and JavaScript only. .NET teams (including Biztactix) have no supported client. This repository is an unofficial C# SDK, started by Ryan at Biztactix while waiting for TypeSafe AI API access, ported from the official SDKs at v0.6.0 so the behaviour (retry, error mapping, headers, wire format) matches the originals.

## Who it is for

- .NET developers calling the TypeSafe AI System One endpoint from services, Azure Functions, workers, and console tools.
- Initially Biztactix's own MSP tooling; published to GitHub (and later NuGet) so other .NET users can adopt it.

## Principles

1. **Wire parity first.** Requests and responses must be byte-compatible with the Python and JS SDKs at the same version. Divergence is a bug unless recorded in DECISIONS.md.
2. **Idiomatic C#, not transliterated Python.** Async only, `CancellationToken` everywhere, records, `ILogger`, `HttpClient` injection. Where C#'s type system can do more than the originals (generics, enums), use it.
3. **Dependency-light.** The core package depends only on `Microsoft.Extensions.Logging.Abstractions`. DI or hosting integrations go in separate packages.
4. **Trim and AOT safe.** The SDK should run under `PublishAot` and `PublishTrimmed`; reflection is confined to clearly annotated convenience overloads.
5. **Honest about status.** The README states the SDK is unofficial and, until API access exists, tested only against a stubbed HTTP handler.

## Success in 6–12 months

- Verified against the live TypeSafe AI API with an integration test suite gated on `TYPESAFE_API_KEY`.
- Published to NuGet as `TypeSafe.Sdk` with a CI pipeline (build, test, pack, publish on tag).
- Typed question/answer API (`Question<TAnswer>`, enum-typed choices and scores) so C# users get compile-time answer types comparable to the JS SDK's inference.
- Tracks upstream SDK releases; version numbers mirror the upstream API version.

## Roadmap

| Phase | Scope | Status |
|-------|-------|--------|
| 0.6.0 | Direct port of Python/JS SDKs, xunit suite against stub handler, demo app | Done (commit 97196d6) |
| Next | Retarget to net10.0, AOT compatibility, naming cleanup, `Content` type replacing `object?` inputs | Planned (epics TSDK-1, TSDK-2) |
| Then | Source-generated System.Text.Json parsing; `Question<TAnswer>` and enum-typed answers | Proposed (see typing review artifact) |
| Later | Live integration tests, NuGet publishing pipeline, DI package | Blocked on API access / not started |

---
*Last reviewed: 2026-09-18*
