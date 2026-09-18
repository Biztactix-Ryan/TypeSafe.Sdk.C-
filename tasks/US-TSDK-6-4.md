---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-5-6
id: US-TSDK-6-4
points: 1
status: done
story_id: US-TSDK-6
tags: []
title: Annotate Content.From(object) with RequiresUnreferencedCode and RequiresDynamicCode
updated: '2026-09-18'
---

On Content.From(object?) in src/TypeSafe.Sdk/Content.cs add [RequiresUnreferencedCode] and [RequiresDynamicCode] with a message such as "Reflection-based serialization is not trim or AOT safe; use Content.From<T>(value, JsonTypeInfo<T>) with a JsonSerializerContext." Push the same attributes down onto Internal/JsonContent.From so the analyzer chain is clean. Acceptance: the attributes are present and name the typed alternative; the build stays warning-free because only annotated code calls the annotated method.