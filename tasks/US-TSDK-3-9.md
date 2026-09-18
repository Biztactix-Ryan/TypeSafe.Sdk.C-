---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-1-5
id: US-TSDK-3-9
points: 1
status: done
story_id: US-TSDK-3
tags: []
title: Convert TypeSafeClientOptions to init setters and recompile README examples
updated: '2026-09-18'
---

In src/TypeSafe.Sdk/TypeSafeClientOptions.cs change settable properties to `init`. Fix ClientConfigTests and the demo if they mutate options after construction. Re-check every README code sample against the new surface from this story (RetryPolicy names, StatusCode, IReadOnlyList models, init setters) and fix them. Acceptance: README examples compile when pasted into the demo; tests pass.