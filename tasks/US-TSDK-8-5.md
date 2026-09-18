---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-7-6
- US-TSDK-3-7
id: US-TSDK-8-5
points: 3
status: done
story_id: US-TSDK-8
tags: []
title: Deserialize response bodies in Transport through TypeSafeJsonContext and map
  JsonException.Path to FieldPath
updated: '2026-09-18'
---

In src/TypeSafe.Sdk/Internal/Transport.cs replace the JsonContent.ParseLenient + Wire path (line ~107 onward) with JsonSerializer.Deserialize(text, TypeSafeJsonContext.Default.<Body>). Catch JsonException and raise TypeSafeApiResponseValidationException with FieldPath derived from JsonException.Path with the leading `$.` stripped (and `$` alone mapped to the empty/root path), keeping status code, body, headers, request id and endpoint exactly as today (src/TypeSafe.Sdk/Exceptions.cs line ~220). Missing required members must surface as validation exceptions, not JsonException leaks. Acceptance: ErrorTests and ResponseTests validation cases pass with reconciled FieldPaths.