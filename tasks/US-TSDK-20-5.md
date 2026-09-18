---
archived: false
assignee: null
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-17-4
- US-TSDK-19-5
id: US-TSDK-20-5
points: 2
status: todo
story_id: US-TSDK-20
tags: []
title: Make TypeSafeClientOptions and RequestOptions sealed records with init setters
updated: '2026-09-18'
---

Convert src/TypeSafe.Sdk/TypeSafeClientOptions.cs and src/TypeSafe.Sdk/RequestOptions.cs from `public sealed class` to `public sealed record` keeping every property `{ get; init; }` and its default (TimeProvider included from US-TSDK-17-4); Usage and ModelMetadata are already records. Preserve all validation/resolution code paths (option resolution code > env > defaults happens in the client, not the record). Update any `new TypeSafeClientOptions { ... }` / `with` usage that changes meaning, README Configuration prose if it says class, and the public API snapshot (accept the diff per CONTRIBUTING.md and keep the verified file updated). Record the change (value equality and `with` on options; breaking for subclassing, which was already sealed) in .project/DECISIONS.md and README Differences. Acceptance: both are sealed records with init-only properties; ClientConfigTests and RequestTests pass unchanged apart from any `with` usage; PublicApiTests re-approved; zero-warning build.