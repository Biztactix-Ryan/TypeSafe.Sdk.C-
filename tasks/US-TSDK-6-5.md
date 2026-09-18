---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-6-4
id: US-TSDK-6-5
points: 2
status: done
story_id: US-TSDK-6
tags: []
title: Document the reflection overload in README and switch the demo to Content.From<T>
  with a JsonSerializerContext
updated: '2026-09-18'
---

README: add a Content section explaining the three implicit inputs, that Content.From(object) uses reflection with camelCase property naming and warns under trimming, and show Content.From<T> with a [JsonSerializable] JsonSerializerContext. Demo: in examples/TypeSafe.Sdk.Demo/Program.cs declare a small record for the state, a DemoJsonContext, and pass Content.From(state, DemoJsonContext.Default.DemoState). Acceptance: README documents both paths; the demo compiles and uses From<T>.