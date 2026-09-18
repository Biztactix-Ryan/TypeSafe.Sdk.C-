---
archived: false
assignee: null
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-22-4
id: US-TSDK-22-6
points: 1
status: todo
story_id: US-TSDK-22
tags: []
title: Document installation and the nuget.org registry decision
updated: '2026-09-18'
---

README: add an Installation section with `dotnet add package TypeSafe.Sdk` and the package id; keep the Requirements section. .project/DECISIONS.md: record the registry decision (nuget.org, secret NUGET_API_KEY, release on v* tags, version must match csproj/constant). .project/INFRASTRUCTURE.md: update the Distribution row and the Deployment Process to describe the tag-driven release and name the secret. Acceptance: all three documents updated consistently; README samples still compile.