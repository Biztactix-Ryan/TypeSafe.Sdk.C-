---
acceptance_criteria:
- A workflow at .github/workflows/ci.yml runs dotnet build, dotnet test and dotnet
  pack with warnings as errors
- The workflow fails if TypeSafe.Sdk.csproj Version and TypeSafeConstants.Version
  differ
- The README shows a build status badge
created: '2026-09-18'
depends_on: []
epic_id: EPIC-TSDK-7
id: US-TSDK-21
points: 3
priority: should
status: ready
tags:
- ci
title: GitHub Actions build, test and pack on push and PR
updated: '2026-09-18'
---

As a maintainer, I want every push and pull request to build, test and pack the library on ubuntu-latest with the .NET 10 SDK so that regressions are caught before merge.