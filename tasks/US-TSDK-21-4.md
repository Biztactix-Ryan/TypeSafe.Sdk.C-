---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on: []
id: US-TSDK-21-4
points: 1
status: done
story_id: US-TSDK-21
tags: []
title: Bump the package version to 0.7.0 and add a version-match check
updated: '2026-09-18'
---

Set <Version>0.7.0</Version> in src/TypeSafe.Sdk/TypeSafe.Sdk.csproj and TypeSafeConstants.Version = "0.7.0" in src/TypeSafe.Sdk/Constants.cs. Add a checked-in script `scripts/check-version.sh` (POSIX sh) that extracts both values and exits non-zero with a clear message when they differ; document it in CLAUDE.md Commands and PROJECT.md. Confirm the README statement that 0.6.0 is the last net8.0 release still reads correctly. Acceptance: script passes on the tree, fails when one value is edited (mutation check, reverted); build and tests green.