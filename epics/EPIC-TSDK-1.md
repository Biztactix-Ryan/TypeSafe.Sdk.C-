---
created: '2026-09-18'
id: EPIC-TSDK-1
points: null
priority: must
status: draft
tags:
- typing-review
- step-1
- breaking
target_date: null
title: Retarget to net10.0, AOT compatibility, naming cleanup
updated: '2026-09-18'
---

Step 1 of the typing review. Move the library from net8.0 to a single net10.0 target (C# 14), drop the RollForward workaround in tests/demo, enable IsAotCompatible so the trim/AOT analyzers flag the remaining reflection, and apply the naming table: RetryConnectionErrors/RetryTimeouts/RetryWhen on RetryPolicy, StatusCode (HttpStatusCode) replacing the int Status on exceptions and responses, ListModelsResponse implementing IReadOnlyList<ModelMetadata>, Probability/Label aliases on answers, init setters on TypeSafeClientOptions.

Success: `dotnet build` and `dotnet test` green on net10.0 with zero warnings (including AOT analyzer warnings, except the one annotated reflection overload), README and CLAUDE.md updated, DECISIONS.md entry marked done. Breaking change; no consumers yet.