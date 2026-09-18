---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-1-5
id: US-TSDK-4-5
points: 2
status: done
story_id: US-TSDK-4
tags: []
title: Add the Content readonly record struct with implicit conversions and parented-node
  cloning
updated: '2026-09-18'
---

Create src/TypeSafe.Sdk/Content.cs: `public readonly record struct Content(JsonNode? Node)` with implicit conversions from string, JsonObject and JsonArray, and a static `Content.Null` (or default) for absent values. When the incoming JsonNode already has a Parent, DeepClone it so the same node can be reused across questions without System.Text.Json throwing. XML-doc the type as "text, JSON object or JSON array". Acceptance: conversions compile; a parented node passed twice does not throw.