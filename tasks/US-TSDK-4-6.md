---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-4-5
id: US-TSDK-4-6
points: 1
status: done
story_id: US-TSDK-4
tags: []
title: Add Content.From<T>(T, JsonTypeInfo<T>) and move the reflection From(object)
  onto Content
updated: '2026-09-18'
---

On Content add `public static Content From<T>(T value, JsonTypeInfo<T> typeInfo)` using JsonSerializer.SerializeToNode(value, typeInfo) (no reflection). Add `public static Content From(object? value)` that delegates to the existing reflection path in Internal/JsonContent.From (camelCase naming preserved). The trim/AOT attributes on the reflection overload are added in US-TSDK-6, not here. Acceptance: From<T> works with a JsonSerializerContext-generated JsonTypeInfo; From(object) matches JsonContent.From output.