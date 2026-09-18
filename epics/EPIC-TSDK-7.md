---
created: '2026-09-18'
id: EPIC-TSDK-7
points: null
priority: should
status: draft
tags:
- release
- ci
- nuget
- integration
target_date: null
title: 'Release readiness: CI, NuGet publishing, live integration tests, DI package'
updated: '2026-09-18'
---

Everything flagged outside the typing review that stands between this repo and a package other .NET teams can adopt: a GitHub Actions workflow (build, test, pack on push/PR; publish on v* tag) with a version-consistency check between the csproj and TypeSafeConstants.Version; a public API snapshot test (PublicApiGenerator + Verify) so breaking changes are deliberate, mirroring the Python SDK's surface snapshot; an integration test project gated on TYPESAFE_API_KEY that exercises /v1/models and /v1/systemone against the live API once access exists; and a separate TypeSafe.Sdk.DependencyInjection package with services.AddTypeSafeClient(...) using IHttpClientFactory and IOptions<TypeSafeClientOptions> so the core stays dependency-light.

Success: a tagged release produces a NuGet package automatically; the README install instructions point at it; the live suite passes with a real key; DI registration documented.