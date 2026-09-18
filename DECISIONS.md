# TypeSafe.Sdk — Decisions

Newest first.

## Retarget to net10.0 and adopt C# 14 features
- **Date**: 2026-09-18
- **Status**: Accepted (epic TSDK-1)
- **Context**: .NET 9 (STS) left support in May 2026 and .NET 8 (LTS) leaves in November 2026. The port targets net8.0 and cannot use C# 13/14 or the .NET 9 System.Text.Json features (`AllowOutOfOrderMetadataProperties`, `RespectRequiredConstructorParameters`, `JsonSerializerOptions.Web`).
- **Decision**: Single `net10.0` target; enable `IsAotCompatible`; apply the naming table from the typing review.
- **Consequences**: Drops the `RollForward=Major` workaround. Consumers on .NET 8 cannot use versions after this change.

## Replace `object?` inputs with a `Content` value type
- **Date**: 2026-09-18
- **Status**: Accepted (epic TSDK-2)
- **Context**: Builders and `state` accept `object?` and run reflection serialisation with camelCase naming. That is the only untyped, trim-unsafe part of the surface and it renames consumer properties silently.
- **Decision**: `readonly record struct Content(JsonNode? Node)` with implicit conversions from `string`, `JsonObject`, `JsonArray`; `Content.From<T>(T, JsonTypeInfo<T>)` for source-generated callers; the reflection overload kept but annotated `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]`.
- **Consequences**: Breaking change to builder signatures. Anonymous-object callers must use `Content.From(obj)` explicitly.

## Async-only public API
- **Date**: 2026-09-18
- **Status**: Accepted
- **Context**: Python ships sync and async clients. Sync-over-async in .NET is a deadlock hazard.
- **Decision**: `SystemOneAsync` and `ListAsync` only, with `CancellationToken`.
- **Consequences**: Documented in README "Differences" section.

## Answer property names mirror the wire format
- **Date**: 2026-09-18
- **Status**: Accepted
- **Context**: `NoulAnswer.Noul` and `ChoiceAnswer.Choice` read oddly in C#, but match the docs and the other SDKs.
- **Decision**: Keep wire names; add `Probability` / `Label` aliases later rather than rename.
- **Consequences**: Cross-SDK docs stay applicable.

## Caller cancellation is `OperationCanceledException`
- **Date**: 2026-09-18
- **Status**: Accepted
- **Context**: JS has `APIUserAbortError`; .NET convention is the framework exception.
- **Decision**: Rethrow as `OperationCanceledException` carrying the caller's token; timeouts are `TypeSafeApiTimeoutException`.

## Retry budget (`TotalTimeout`) present but off by default
- **Date**: 2026-09-18
- **Status**: Accepted
- **Context**: Python defaults to a 30 s retry budget; JS has none.
- **Decision**: Expose `RetryPolicy.TotalTimeout`, default `null`, matching JS.

## Score criteria minimum is one
- **Date**: 2026-09-18
- **Status**: Accepted
- **Context**: Python requires ≥1 criterion, JS requires ≥2 at the same version.
- **Decision**: Follow the permissive Python rule; the server is the arbiter.

## Supplied `HttpClient` is not disposed unless `DisposeHttpClient` is set
- **Date**: 2026-09-18
- **Status**: Accepted
- **Context**: Python closes a supplied client; .NET convention is not to dispose what you don't own (`IHttpClientFactory` clients especially).
- **Decision**: Opt-in disposal.

## Hand-written JSON parsing (JsonNode) for the initial port
- **Date**: 2026-09-18
- **Status**: Superseded by the typing review recommendation (source-generated `JsonSerializerContext`, planned after TSDK-1/2)
- **Context**: On net8.0, System.Text.Json polymorphism needs the discriminator first and cannot fall back for unknown types cleanly; the Python SDK's forward-compat rules required a manual parser.
- **Decision**: `Wire` helper with explicit field paths.
- **Consequences**: ~200 lines to delete once on .NET 10.

---
*Last reviewed: 2026-09-18*
