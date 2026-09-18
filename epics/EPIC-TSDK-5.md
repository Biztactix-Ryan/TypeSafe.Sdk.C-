---
created: '2026-09-18'
id: EPIC-TSDK-5
points: null
priority: should
status: active
tags:
- typing-review
- step-5
- generics
target_date: null
title: Enum-typed Choice<TEnum> and Score<TEnum>
updated: '2026-09-18'
---

Step 5 of the typing review. `Question.Choice<TEnum>(name, instructions)` derives labels from the enum ([JsonStringEnumMemberName] override, JsonNamingPolicy.SnakeCaseLower default) and descriptions from [Description]; `ChoiceAnswer<TEnum>` carries `TEnum Choice` and `IReadOnlyDictionary<TEnum,double> Probabilities`; an undeclared label is a validation error at answers.<name>.choice. `Question.Score<TEnum>` validates contiguous values from zero, sends [Description] texts as the rubric, and `ScoreAnswer<TEnum>` exposes Legend/Probabilities keyed by the enum plus `Nearest`.

Success: demo showcases Tone and Urgency enums; tests cover label mapping both directions, description attributes, non-contiguous enum rejection and unknown-label errors; untyped ChoiceAnswer/ScoreAnswer remain for dynamic sets. Non-breaking (additive).