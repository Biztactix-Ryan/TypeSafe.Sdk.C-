# TypeSafe.Sdk — Architecture

## Shape

A single class library (`src/TypeSafe.Sdk`, namespace `TypeSafe`), no services, no database. It is an HTTP client wrapper around two TypeSafe AI endpoints plus the types needed to build requests and read responses.

## Service map

| Unit | Kind | Talks to | Notes |
|------|------|----------|-------|
| `TypeSafe.Sdk` | Class library (net8.0 today, net10.0 planned) | `https://api.typesafe.ai` over HTTPS | The product |
| `TypeSafe.Sdk.Tests` | xunit test project | The library via a stub `HttpMessageHandler` | No network |
| `TypeSafe.Sdk.Demo` | Console app | The library, live API | Needs `TYPESAFE_API_KEY` |

## Endpoints consumed

| Method | Path | SDK entry point | Response type |
|--------|------|-----------------|---------------|
| POST | `/v1/systemone` | `TypeSafeClient.SystemOneAsync` | `SystemOneResponse` |
| GET | `/v1/models` | `TypeSafeClient.Models.ListAsync` | `ListModelsResponse` |

Wire contract is defined by the upstream OpenAPI schema (`https://api.typesafe.ai/openapi.json`) as mirrored in the Python SDK's generated `_schemas/models.py`.

## Internal layering

```
TypeSafeClient / ModelsResource        public surface, option resolution
        │
Internal/Transport                     header merge, retry loop, timeout, logging, error mapping
        │
Internal/JsonContent, Responses.Wire   serialise questions, lenient body decode, strict response parse
        │
HttpClient (owned or supplied)         one attempt per HttpRequestMessage
```

Cross-cutting pieces: `RetryPolicy` (record, validated on use), `Internal/RetryAfterParser`, `Internal/HeaderSnapshot` (case-insensitive header copies for exceptions and responses), `Internal/Logging` (`SdkLog` level filter, `ConsoleLogger` default, `Redaction`), `Internal/RuntimeInfo` (`X-TypeSafe-Runtime` header value).

## Contracts that cross boundaries

- **Request headers** set by the SDK and protected from override: `Authorization: Bearer`, `Accept`, `Content-Type`, `User-Agent`, `X-TypeSafe-SDK`, `X-TypeSafe-Runtime`, `X-TypeSafe-Retry-Count`.
- **Response header** `x-typesafe-request-id` surfaces as `RequestId` on responses and exceptions.
- **Exceptions**: `TypeSafeException` base; `TypeSafeApiException` and per-status subclasses for non-2xx; `TypeSafeApiResponseValidationException` (2xx with bad shape, carries `FieldPath`); `TypeSafeApiConnectionException` and `TypeSafeApiTimeoutException` for no-response failures; caller cancellation is the standard `OperationCanceledException`.
- **Forward compatibility**: unknown answer types are skipped with a warning; unknown JSON fields are ignored.

## Standards

- **Async only**, every public call takes a `CancellationToken`.
- **Logging** through `Microsoft.Extensions.Logging.ILogger`; credential headers redacted, bodies not.
- **Errors** are exceptions, never result codes; messages include endpoint, status, detail, request id.
- **Tests** are xunit against `StubHandler`; environment-variable tests disable parallelisation for the assembly.
- **Public API changes** are deliberate and noted in DECISIONS.md while the package is pre-1.0.

---
*Last reviewed: 2026-09-18*
