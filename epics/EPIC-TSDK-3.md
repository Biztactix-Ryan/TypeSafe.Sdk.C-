---
created: '2026-09-18'
id: EPIC-TSDK-3
points: null
priority: must
status: done
tags:
- typing-review
- step-3
- aot
- json
target_date: null
title: Source-generated System.Text.Json replaces the hand parser
updated: '2026-09-18'
---

Step 3 of the typing review. Model Answer as a non-abstract record with [JsonPolymorphic(TypeDiscriminatorPropertyName="type", AllowOutOfOrderMetadataProperties=true, UnknownDerivedTypeHandling=FallBackToBaseType)] and [JsonDerivedType] for noul/choice/score; NoulAnswer/ChoiceAnswer/ScoreAnswer as positional records; SystemOneBody and ModelList records; a TypeSafeJsonContext with RespectNullableAnnotations and RespectRequiredConstructorParameters. Serialize questions through the same context. Map JsonException.Path to TypeSafeApiResponseValidationException.FieldPath. Delete Wire, the hand Parse methods and ResponseFieldException.

Success: all existing response/validation tests pass with their field-path expectations reconciled; unknown answer types still produce a warning and are skipped; the library builds with IsAotCompatible and no IL2xxx/IL3xxx warnings; ~200 lines removed. Non-breaking for callers.