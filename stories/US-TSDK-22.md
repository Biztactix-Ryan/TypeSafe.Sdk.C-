---
acceptance_criteria:
- A workflow triggered by v* tags packs with the tag version and pushes to the chosen
  registry using a repository secret named in INFRASTRUCTURE.md
- The package includes README, license expression, symbols and SourceLink
- README install instructions reference the package id and the registry decision is
  recorded in DECISIONS.md
created: '2026-09-18'
depends_on: []
epic_id: EPIC-TSDK-7
id: US-TSDK-22
points: 3
priority: should
status: backlog
tags:
- nuget
- release
title: Publish to NuGet on version tags
updated: '2026-09-18'
---

As a .NET developer, I want to install TypeSafe.Sdk from NuGet so that I can adopt it without cloning the repo.