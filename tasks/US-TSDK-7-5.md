---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-1-5
- US-TSDK-3-8
id: US-TSDK-7-5
points: 3
status: done
story_id: US-TSDK-7
tags: []
title: Declare Answer, NoulAnswer, ChoiceAnswer and ScoreAnswer as polymorphic records
updated: '2026-09-18'
---

In src/TypeSafe.Sdk/Responses.cs (lines ~32-108) rewrite the answer hierarchy as records: `Answer` non-abstract with [JsonPolymorphic(TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)] and [JsonDerivedType(typeof(NoulAnswer), "noul")] etc., plus a [JsonExtensionData] IDictionary<string, JsonElement> property for unknown fields. ScoreAnswer legend and probabilities become IReadOnlyDictionary<int, T> (integer keys). Keep the Probability/Label aliases from US-TSDK-3 marked [JsonIgnore]. Constructors must accept out-of-order `type` (AllowOutOfOrderMetadataProperties is set on the context in the next task). Acceptance: records compile; the shapes match the current parser output for every ResponseTests fixture.