---
acceptance_criteria:
- Content has implicit conversions from string, JsonObject and JsonArray and a Node
  property
- Content.From<T>(T value, JsonTypeInfo<T> typeInfo) serializes without reflection
- A parented JsonNode passed to Content is cloned so reuse across questions never
  throws
- Unit tests cover null, string, object, array and typed From<T> paths
created: '2026-09-18'
depends_on: []
epic_id: EPIC-TSDK-2
id: US-TSDK-4
points: 3
priority: must
status: backlog
tags:
- step-2
title: Introduce the Content value type
updated: '2026-09-18'
---

As an SDK author, I want a readonly record struct Content wrapping JsonNode with implicit conversions so that "text, JSON object, or array" inputs are a real type instead of object?.