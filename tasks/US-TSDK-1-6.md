---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-1-5
id: US-TSDK-1-6
points: 1
status: done
story_id: US-TSDK-1
tags: []
title: Use JsonSerializerOptions.Web in JsonContent instead of a new options instance
updated: '2026-09-18'
---

In src/TypeSafe.Sdk/Internal/JsonContent.cs replace `new JsonSerializerOptions(JsonSerializerDefaults.Web)` (line ~10) with the .NET 9+ singleton `JsonSerializerOptions.Web`. Confirm existing QuestionTests and RequestTests expected JSON is unchanged. Acceptance: no new options instance is allocated; tests pass.