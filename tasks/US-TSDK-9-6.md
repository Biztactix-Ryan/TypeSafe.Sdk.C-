---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-9-5
- US-TSDK-5-6
id: US-TSDK-9-6
points: 1
status: done
story_id: US-TSDK-9
tags: []
title: Serialize the SystemOne request body through the context, passing RawQuestion
  JSON through after validation
updated: '2026-09-18'
---

In src/TypeSafe.Sdk/Internal/Transport.cs (line ~58) and the request assembly in TypeSafeClient.cs, serialize the request body with TypeSafeJsonContext instead of JsonContent.Serialize(object). RawQuestion (Questions.cs ~232) must still validate `type` and then write its JsonObject verbatim (a custom JsonConverter<RawQuestion> or a JsonNode-typed body property). Acceptance: RequestTests wire bodies unchanged; RawQuestion passthrough test passes; JsonContent.Serialize(object) has no remaining callers.