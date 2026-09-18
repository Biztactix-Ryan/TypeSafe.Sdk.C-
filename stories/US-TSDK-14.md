---
acceptance_criteria:
- Labels come from JsonStringEnumMemberName when present, otherwise JsonNamingPolicy.SnakeCaseLower
- Description attributes on enum members are sent as criteria descriptions
- ChoiceAnswer<TEnum> exposes TEnum Choice and IReadOnlyDictionary<TEnum, double>
  Probabilities
- A label not declared on the enum raises TypeSafeApiResponseValidationException at
  answers.<name>.choice
created: '2026-09-18'
depends_on: []
epic_id: EPIC-TSDK-5
id: US-TSDK-14
points: 5
priority: should
status: backlog
tags:
- step-5
- generics
title: Enum-typed choice questions and answers
updated: '2026-09-18'
---

As a caller with a closed label set, I want Question.Choice<Tone>("tone", ...) to derive labels from my enum and return ChoiceAnswer<Tone> so that probabilities are keyed by enum members and typos are compile errors.