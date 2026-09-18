---
acceptance_criteria:
- Content.From(object) carries RequiresUnreferencedCode and RequiresDynamicCode with
  a message naming the typed alternative
- README documents the camelCase naming behaviour of the reflection overload and shows
  Content.From<T> with a JsonSerializerContext
- The demo uses the typed From<T> path
created: '2026-09-18'
depends_on:
- US-TSDK-5
epic_id: EPIC-TSDK-2
id: US-TSDK-6
points: 2
priority: should
status: done
tags:
- step-2
- aot
- docs
title: Keep an annotated reflection overload for anonymous objects
updated: '2026-09-18'
---

As a caller writing a quick script, I want Content.From(new { document = "..." }) to keep working so that anonymous objects remain convenient, while trimmed apps get a compile-time warning instead of a runtime failure.