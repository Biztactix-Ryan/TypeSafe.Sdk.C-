---
acceptance_criteria:
- No public builder, request or client method takes object? except Content.From(object)
- Question.Score("urgency", "How urgent?", ["can wait", "today"]) compiles and serializes
  as before
- Existing question serialization tests pass unchanged in their expected JSON
- The generic Score<TDescription> and Choice<TDescription> overloads are removed
created: '2026-09-18'
depends_on:
- US-TSDK-3
- US-TSDK-4
epic_id: EPIC-TSDK-2
id: US-TSDK-5
points: 5
priority: must
status: done
tags:
- step-2
- breaking
title: Replace object? parameters with Content on builders and requests
updated: '2026-09-18'
---

As a caller, I want Question.Noul/Choice/Score, NoulCriteria, SystemOneRequest.State and SystemOneAsync to take Content so that the compiler tells me what is accepted and collection expressions of strings still work for score criteria.