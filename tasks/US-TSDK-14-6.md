---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-14-5
- US-TSDK-12-6
id: US-TSDK-14-6
points: 3
status: done
story_id: US-TSDK-14
tags: []
title: Add ChoiceQuestion<TEnum>, ChoiceAnswer<TEnum> and Question.Choice<TEnum>
updated: '2026-09-18'
---

Add `public sealed record ChoiceAnswer<TEnum>(TEnum Choice, double Confidence, IReadOnlyDictionary<TEnum, double> Probabilities) : Answer where TEnum : struct, Enum` (not registered in the JSON context; it is built on the response side) and a builder `Question.Choice<TEnum>(string name, Content instructions = default)` returning Named<ChoiceAnswer<TEnum>> whose underlying question serializes as a plain choice question with criteria = labels (with Description text as the criteria description when present, null otherwise) so the wire format is unchanged. On the response side extend SystemOneResponse.Get so that a Named<ChoiceAnswer<TEnum>> converts the wire ChoiceAnswer via the EnumLabels reverse lookup; a label not declared on the enum throws TypeSafeApiResponseValidationException with FieldPath `answers.<name>.choice` (carry status/body/headers/request id/endpoint from the response as the transport does). Tests cover the criteria JSON, the typed conversion including enum-keyed probabilities, and the undeclared-label failure. Acceptance: zero-warning build; tests pass; README not yet (US-TSDK-16).