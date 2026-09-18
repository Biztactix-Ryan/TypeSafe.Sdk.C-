---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on: []
id: US-TSDK-1-5
points: 1
status: done
story_id: US-TSDK-1
tags: []
title: Retarget the three csproj files to net10.0 and drop RollForward
updated: '2026-09-18'
---

Change <TargetFramework> from net8.0 to net10.0 in src/TypeSafe.Sdk/TypeSafe.Sdk.csproj, tests/TypeSafe.Sdk.Tests/TypeSafe.Sdk.Tests.csproj and examples/TypeSafe.Sdk.Demo/TypeSafe.Sdk.Demo.csproj. Remove the <RollForward>Major</RollForward> element from the tests and demo projects. Bump Microsoft.Extensions.Logging.Abstractions (and the test SDK packages if needed) to versions that target net10.0. Acceptance: `dotnet build` and `dotnet test` succeed with zero warnings on the .NET 10 SDK.