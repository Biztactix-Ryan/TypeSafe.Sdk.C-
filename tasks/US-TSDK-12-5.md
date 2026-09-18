---
archived: false
assignee: null
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-11-6
- US-TSDK-11-7
id: US-TSDK-12-5
points: 3
status: todo
story_id: US-TSDK-12
tags: []
title: Add the SystemOneAsync overload that takes named questions
updated: '2026-09-18'
---

On ITypeSafeClient and TypeSafeClient (src/TypeSafe.Sdk/TypeSafeClient.cs) add `Task<SystemOneResponse> SystemOneAsync(Content state, params ReadOnlySpan<INamedQuestion> questions)` plus a companion overload carrying `string? model = null, RequestOptions? options = null, CancellationToken cancellationToken = default` (C# 13 params ReadOnlySpan works with a collection expression `[billing, tone]`). Also add a SystemOneRequest constructor taking IEnumerable<INamedQuestion>. Build the wire dictionary from each Name/Question; a duplicate name throws TypeSafeException naming it. The existing dictionary-based overload stays. Acceptance: `await client.SystemOneAsync(state, [billing, tone])` compiles and sends the same body as the dictionary form (RequestTests asserts byte-equality of the recorded bodies); PublicSurfaceTests still passes (no object? parameters); zero-warning build.