---
acceptance_criteria:
- SystemOneAsync has an overload taking params ReadOnlySpan<INamedQuestion> that works
  with a collection expression
- Get<TAnswer>(Named<TAnswer>) returns the typed answer for a matching name and type
- Get throws TypeSafeException naming both the actual and requested answer types when
  they differ, and a distinct message when the name is absent
- Answers, Nouls, Choices and Scores remain available
created: '2026-09-18'
depends_on:
- US-TSDK-11
epic_id: EPIC-TSDK-4
id: US-TSDK-12
points: 5
priority: must
status: done
tags:
- step-4
- generics
title: SystemOneAsync accepts named questions and response.Get returns typed answers
updated: '2026-09-18'
---

As a caller, I want to pass [billing, tone] to SystemOneAsync and read response.Get(tone) as a ChoiceAnswer so that answer types are known at compile time and wrong lookups fail with a precise message.