# TypeSafe.Sdk — Infrastructure

This is a library, not a service. "Environments" are the API endpoints the SDK talks to and the places the package is distributed.

## Environments

| Environment | URL | Purpose | Notes |
|------------|-----|---------|-------|
| Development | local `dotnet build` / `dotnet test` | Build and unit test | No network; stub `HttpMessageHandler` |
| Live API | `https://api.typesafe.ai` | The only TypeSafe AI endpoint | Needs `TYPESAFE_API_KEY`; overridable with `TYPESAFE_BASE_URL` |
| Distribution | https://github.com/Biztactix-Ryan/TypeSafe.Sdk.C- | Source of record | NuGet publishing: TBD (ask: Ryan — nuget.org vs GitHub Packages) |

## CI/CD

### Build Pipeline

None yet. TBD: GitHub Actions workflow running `dotnet build`, `dotnet test`, `dotnet pack` on push and PR; publish on `v*` tag.

### Deployment Process

Manual: `dotnet pack src/TypeSafe.Sdk/TypeSafe.Sdk.csproj -c Release -o artifacts` produces `TypeSafe.Sdk.<version>.nupkg`. Version lives in the csproj `<Version>` and `TypeSafeConstants.Version`; both must match (mirrors upstream `check:version`).

### Rollback Procedure

NuGet packages are immutable; roll back by publishing a patch version or unlisting. Not applicable until publishing exists.

## Hosting & Services

- Source: GitHub, `main` branch, owner Biztactix-Ryan.
- Package registry: TBD.
- External service consumed: TypeSafe AI API (HTTPS, bearer auth).

## Monitoring & Alerting

Not applicable to the library itself. Consumers get `ILogger` output (`info` = request summaries with status, elapsed ms, request id; `debug` = redacted headers and bodies) and `RequestId` on every response and exception for correlating with TypeSafe support.

## Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `TYPESAFE_API_KEY` | Yes (unless passed in code) | Bearer token for the API |
| `TYPESAFE_BASE_URL` | No | API root override; default `https://api.typesafe.ai` |
| `TYPESAFE_DEFAULT_MODEL` | No | Default model; default `jev-latest` |
| `TYPESAFE_LOG_LEVEL` | No | `debug`, `info`, `warn`, `error`, `off`; default `warn` |

## Backup & Recovery

Git history on GitHub is the backup. No data is stored by the SDK.

---
*Last reviewed: 2026-09-18*
