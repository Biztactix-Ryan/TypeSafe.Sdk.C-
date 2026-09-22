# TypeSafe.Sdk — Infrastructure

This is a library, not a service. "Environments" are the API endpoints the SDK talks to and the places the package is distributed.

## Environments

| Environment | URL | Purpose | Notes |
|------------|-----|---------|-------|
| Development | local `dotnet build` / `dotnet test` | Build and unit test | No network; stub `HttpMessageHandler` |
| Live API | `https://api.typesafe.ai` | The only TypeSafe AI endpoint | Needs `TYPESAFE_API_KEY`; overridable with `TYPESAFE_BASE_URL` |
| Distribution | https://github.com/Biztactix-Ryan/TypeSafe.Sdk.C- | Source of record | Both packages published to nuget.org from `v*` tags |

## CI/CD

### Build Pipeline

`.github/workflows/ci.yml` — workflow **CI**, one job (`build`, "Build, test and pack") on `ubuntu-latest`.

- **Triggers**: push to `main`, and every `pull_request` (any base branch).
- **Permissions**: `contents: read` at the workflow and job level — the job only reads the repo and uploads an artifact.
- **Concurrency**: grouped per workflow and ref, `cancel-in-progress` only for pull requests, so a new push to a PR supersedes the in-flight run while `main` runs finish.
- **SDK**: `actions/setup-dotnet@v4` with `dotnet-version: 10.0.x`, matching the `net10.0` target. Checkout is `actions/checkout@v4`.
- **Steps, in order**:
  1. `dotnet restore` — solution-wide, `TypeSafe.Sdk.slnx` at the repo root.
  2. `dotnet build --no-restore -warnaserror` — the warnings gate. `Directory.Build.props` keeps `TreatWarningsAsErrors=false` so local builds stay workable; CI passes `-warnaserror` on the command line instead, so the tree must stay at zero warnings.
  3. `dotnet test --no-build` — the xunit suite (258 core + 15 dependency-injection tests today, no network) plus the 3 live tests in `tests/TypeSafe.Sdk.IntegrationTests`, which skip because CI carries no `TYPESAFE_API_KEY` — so 273 passed, 3 skipped.
  4. `scripts/check-version.sh` — fails the run if the three places the version is written disagree: `<Version>` in `src/TypeSafe.Sdk/TypeSafe.Sdk.csproj`, `TypeSafeConstants.Version` in `src/TypeSafe.Sdk/Constants.cs`, and `<Version>` in `src/TypeSafe.Sdk.DependencyInjection/TypeSafe.Sdk.DependencyInjection.csproj`. It takes an optional expected version; CI calls it without one, so it only enforces that the three agree.
  5. "Pack the SDK" — `dotnet pack src/TypeSafe.Sdk/TypeSafe.Sdk.csproj -c Release -o artifacts` — packs in `Release`. No `--no-build`: the build step above produced `Debug`, so pack builds `Release` itself rather than failing on missing assets.
  6. "Pack the dependency-injection package" — `dotnet pack src/TypeSafe.Sdk.DependencyInjection/TypeSafe.Sdk.DependencyInjection.csproj -c Release -o artifacts`, into the same folder. Two explicit pack steps rather than packing the solution, so a new project is never published by accident: adding one to `TypeSafe.Sdk.slnx` does not add it to the release.
  7. `actions/upload-artifact@v4` uploads `artifacts/*.nupkg` — both packages — as the artifact `nupkg`, with `if-no-files-found: error`.
- **Not in this workflow**: publishing to NuGet — that is the tag-driven release workflow below.

The workflow's YAML is parsed locally (PyYAML) and the whole command sequence is reproducible from the repo root; GitHub's first run is the final proof, since `act` is not installed here.

### Deployment Process

**Tag-driven release.** `.github/workflows/release.yml` — workflow **Release**, one job (`release`, "Pack and publish to NuGet") on `ubuntu-latest`. Both packages are packed and published from the same tag.

- **Trigger**: `push` of a tag matching `v*` only. Nothing else runs it; there is no manual dispatch.
- **Registry**: nuget.org (`https://api.nuget.org/v3/index.json`).
- **Secret**: `NUGET_API_KEY`, a repository secret in GitHub → Settings → Secrets and variables → Actions. **A human must create it** (an nuget.org API key scoped to push both `TypeSafe.Sdk` and `TypeSafe.Sdk.DependencyInjection`, or a glob such as `TypeSafe.Sdk*`); until it exists the push step fails and no package is published. The key is never echoed — it is passed straight to `--api-key`.
- **Permissions**: `contents: read` at the workflow and job level. **Concurrency**: `release-<ref>`, `cancel-in-progress: false`, so a publish in flight is never cancelled.
- **Packages**: two, released together from the one tag — `TypeSafe.Sdk` (dependency list exactly `Microsoft.Extensions.Logging.Abstractions`) and `TypeSafe.Sdk.DependencyInjection` (dependency list `TypeSafe.Sdk` at the same version, `Microsoft.Extensions.Http`, `Microsoft.Extensions.Options.ConfigurationExtensions`). They always carry the same version number, so the `TypeSafe.Sdk` dependency the DI package declares is the package published beside it.
- **Steps, in order**: `actions/checkout@v4`; `actions/setup-dotnet@v4` with `10.0.x`; derive `VERSION=${GITHUB_REF_NAME#v}` into `$GITHUB_ENV`; `scripts/check-version.sh "$VERSION"` (the tag must match all three of the csproj `<Version>`, `TypeSafeConstants.Version` and the DI csproj `<Version>`, so a mistagged or half-bumped release fails before anything is published); `dotnet restore`; `dotnet build -c Release --no-restore -warnaserror`; `dotnet test -c Release --no-build`; `dotnet pack src/TypeSafe.Sdk/TypeSafe.Sdk.csproj -c Release --no-build -o artifacts`; `dotnet pack src/TypeSafe.Sdk.DependencyInjection/TypeSafe.Sdk.DependencyInjection.csproj -c Release --no-build -o artifacts`; `dotnet nuget push artifacts/*.nupkg ... --skip-duplicate`; `actions/upload-artifact@v4` uploads `artifacts/*.nupkg` and `artifacts/*.snupkg` as `nupkg-<tag>`.
- Both packs write into the same `artifacts` folder and the single `dotnet nuget push artifacts/*.nupkg` publishes both — the glob is what makes one push step cover both packages, so a third packable project would need only a third pack step. The `NUGET_API_KEY` must therefore be scoped to push both ids (or be a glob/account key).
- `--skip-duplicate` makes a re-run of the same tag a no-op instead of a failure, including the case where one package pushed and the other failed. The `.snupkg`s need no second push: `dotnet nuget push` sends each symbols package alongside its `.nupkg` when both sit in the same folder.
- **Releasing**: bump all three version locations together — the csproj `<Version>`, `TypeSafeConstants.Version` and the DI csproj `<Version>` — run `scripts/check-version.sh <version>`, commit, then tag `v<version>` and push the tag.

**Manual/local**: `dotnet pack src/TypeSafe.Sdk/TypeSafe.Sdk.csproj -c Release -o artifacts` and `dotnet pack src/TypeSafe.Sdk.DependencyInjection/TypeSafe.Sdk.DependencyInjection.csproj -c Release -o artifacts` produce `TypeSafe.Sdk.<version>.nupkg` and `TypeSafe.Sdk.DependencyInjection.<version>.nupkg` (each with its `.snupkg`). The version lives in the two csproj `<Version>`s and `TypeSafeConstants.Version`; all three must match (mirrors upstream `check:version`).

The pack also emits a `.snupkg` symbols package (`IncludeSymbols`/`SymbolPackageFormat`), and the .NET SDK's built-in SourceLink (`PublishRepositoryUrl`/`EmbedUntrackedSources`, no package reference) stamps the pdb with raw.githubusercontent URLs for the HEAD commit.

### Rollback Procedure

NuGet packages are immutable: a published version cannot be replaced. Roll back by unlisting the bad version on nuget.org and publishing a patch version (new commit, new `v*` tag). Deleting the git tag alone does not retract a published package.

## Hosting & Services

- Source: GitHub, `main` branch, owner Biztactix-Ryan.
- Package registry: nuget.org, package ids `TypeSafe.Sdk` and `TypeSafe.Sdk.DependencyInjection`, both published by the Release workflow on `v*` tags, at the same version.
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
