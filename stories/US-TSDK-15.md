---
acceptance_criteria:
- The builder rejects enums whose values are not contiguous from zero with a clear
  TypeSafeException
- Rubric descriptions are taken from Description attributes in enum value order
- ScoreAnswer<TEnum> exposes Legend and Probabilities keyed by the enum and a Nearest
  property rounding Score
- Untyped ScoreAnswer remains available for dynamic rubrics
created: '2026-09-18'
depends_on:
- US-TSDK-11
- US-TSDK-12
epic_id: EPIC-TSDK-5
id: US-TSDK-15
points: 5
priority: should
status: done
tags:
- step-5
- generics
title: Enum-typed score questions and answers
updated: '2026-09-18'
---

As a caller with an ordered rubric, I want Question.Score<Urgency>("urgency", ...) to send the rubric from my enum and return ScoreAnswer<Urgency> with a Nearest level so that scores map back to named buckets.