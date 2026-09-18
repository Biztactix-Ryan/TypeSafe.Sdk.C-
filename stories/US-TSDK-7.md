---
acceptance_criteria:
- Answer is a non-abstract record with AllowOutOfOrderMetadataProperties and FallBackToBaseType
  and a JsonExtensionData property
- TypeSafeJsonContext sets RespectNullableAnnotations and RespectRequiredConstructorParameters
- A body with the type property after other fields deserializes to the correct derived
  record
- Integer-keyed legend and probabilities deserialize to IReadOnlyDictionary<int, T>
created: '2026-09-18'
depends_on: []
epic_id: EPIC-TSDK-3
id: US-TSDK-7
points: 5
priority: must
status: backlog
tags:
- step-3
- json
title: Model answers and bodies as polymorphic records with a JsonSerializerContext
updated: '2026-09-18'
---

As an SDK author, I want Answer, NoulAnswer, ChoiceAnswer, ScoreAnswer, Usage, SystemOneBody and ModelList declared as records with JsonPolymorphic/JsonDerivedType attributes and a source-generated context so that System.Text.Json parses responses without reflection.