---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-5-5
id: US-TSDK-5-6
points: 2
status: done
story_id: US-TSDK-5
tags: []
title: Change SystemOneRequest.State and the SystemOneAsync state parameter to Content
updated: '2026-09-18'
---

In src/TypeSafe.Sdk/TypeSafeClient.cs change `SystemOneRequest(object? state, ...)`, the `State` property and both SystemOneAsync overloads (lines ~13-43 and ~171-180) to take `Content`. Update the XML doc cref on the inheritdoc. Fix RequestTests, ClientConfigTests and examples/TypeSafe.Sdk.Demo/Program.cs (use a JsonObject or string for now; the typed From<T> demo lands in US-TSDK-6). Update README request examples. Acceptance: no public client or request method takes object?; tests pass; demo builds.