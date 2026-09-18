---
archived: false
assignee: null
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-20-6
id: US-TSDK-20-7
points: 1
status: todo
story_id: US-TSDK-20
tags: []
title: Adopt params ReadOnlySpan<T> on builders and the field keyword on lazy properties
updated: '2026-09-18'
---

Switch the remaining `params string[] labels` / `params Content[] criteria` on Question.Choice/Score and Question.Named.Choice/Score to `params ReadOnlySpan<string>` / `params ReadOnlySpan<Content>` (collection expressions and existing call sites keep compiling; copy the span internally). Replace `_x ??= ...` lazy properties (SystemOneResponse Nouls/Choices/Scores caches and any others) with the C# 14 `field` keyword. Check Directory.Build.props LangVersion supports `field` and extension members on the .NET 10 SDK (LangVersion latest/preview as needed; record in DECISIONS.md if you have to set preview). Re-approve the API snapshot. Acceptance: no `params T[]` remains on public builders; no backing-field lazy pattern remains where `field` applies; README samples compile via the usual scratch file; zero-warning build; tests pass.