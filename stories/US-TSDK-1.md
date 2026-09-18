---
acceptance_criteria:
- All three csproj files target net10.0 and no RollForward element remains
- dotnet build and dotnet test succeed with zero warnings
- JsonContent uses JsonSerializerOptions.Web instead of a new options instance
- README, CLAUDE.md and .project/PROJECT.md state the net10.0 requirement
created: '2026-09-18'
depends_on: []
epic_id: EPIC-TSDK-1
id: US-TSDK-1
points: 3
priority: must
status: backlog
tags:
- step-1
title: Retarget all projects to net10.0
updated: '2026-09-18'
---

As a maintainer, I want the library, tests and demo on a single net10.0 target so that C# 14 and the .NET 9/10 System.Text.Json features are available and the RollForward workaround goes away.