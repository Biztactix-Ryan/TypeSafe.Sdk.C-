---
created: '2026-09-18'
id: EPIC-TSDK-2
points: null
priority: must
status: done
tags:
- typing-review
- step-2
- breaking
- aot
target_date: null
title: Content value type replaces object? inputs
updated: '2026-09-18'
---

Step 2 of the typing review. Introduce `readonly record struct Content(JsonNode? Node)` with implicit conversions from string, JsonObject and JsonArray, `Content.From<T>(T, JsonTypeInfo<T>)` for source-generated callers, and a reflection `Content.From(object)` annotated [RequiresUnreferencedCode]/[RequiresDynamicCode]. Replace every `object?` parameter on Question builders, NoulCriteria, SystemOneRequest.State and SystemOneAsync with Content. Collection expressions of strings must still work for Score criteria via element-wise conversion.

Success: no `object?` remains on the public surface except the annotated From overload; JsonContent.From(object) is deleted or internal; tests cover string, JsonNode, collection-expression and typed From<T> paths; README examples updated. Breaking change to builder signatures.