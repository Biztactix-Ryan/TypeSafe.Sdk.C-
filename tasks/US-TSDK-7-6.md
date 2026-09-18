---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-7-5
id: US-TSDK-7-6
points: 2
status: done
story_id: US-TSDK-7
tags: []
title: Declare Usage, SystemOneBody, ModelMetadata and ModelList records and add TypeSafeJsonContext
updated: '2026-09-18'
---

Convert Usage (line ~109), ModelMetadata (~217) and the body shapes behind SystemOneResponse and ListModelsResponse into records (SystemOneBody with answers dictionary, usage, model, request id; ModelList with the models array). Create src/TypeSafe.Sdk/Internal/TypeSafeJsonContext.cs: a partial JsonSerializerContext with [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower or CamelCase as the wire format requires, AllowOutOfOrderMetadataProperties = true, RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true)] and [JsonSerializable] entries for every body, answer and (later) question type. Acceptance: a body with `type` after other fields deserializes to the right derived record; the context options are asserted by a test.