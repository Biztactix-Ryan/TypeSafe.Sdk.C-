---
archived: false
assignee: null
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-25-5
id: US-TSDK-25-6
points: 2
status: todo
story_id: US-TSDK-25
tags: []
title: Package, publish and document the DI package
updated: '2026-09-18'
---

Extend .github/workflows/ci.yml and release.yml so pack (and push on release) covers both src/TypeSafe.Sdk/TypeSafe.Sdk.csproj and src/TypeSafe.Sdk.DependencyInjection/TypeSafe.Sdk.DependencyInjection.csproj (or pack the solution filtered to packable projects); ensure scripts/check-version.sh also covers the DI csproj version if it declares its own <Version>. Verify with a local `dotnet pack` that the DI nuspec depends on TypeSafe.Sdk 0.7.0, Microsoft.Extensions.Http and Options.ConfigurationExtensions, and that the core nuspec's dependency list is still exactly Microsoft.Extensions.Logging.Abstractions. README: add a `## Dependency injection` section (AddTypeSafeClient with configuration, code override, adding a resilience handler via the builder) and compile it via the usual scratch file (the test project for DI can host the scratch); note the package id under Installation. .project/DECISIONS.md: entry for the separate DI package and its dependency policy; INFRASTRUCTURE.md: both packages released from the same tag. Acceptance: workflows valid YAML and pack both; nuspec dependency lists as stated; README section compiles; zero-warning build; tests pass.