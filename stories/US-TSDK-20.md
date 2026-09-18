---
acceptance_criteria:
- Usage, ModelMetadata, RequestOptions and TypeSafeClientOptions are sealed records
  with init setters
- SystemOneResponse.Answers is an OrderedDictionary<string, Answer> preserving wire
  order with index access
- Nouls, Choices and Scores are provided as extension members over IReadOnlyDictionary<string,
  Answer>
- Builders use params ReadOnlySpan<T> and lazy properties use the field keyword
created: '2026-09-18'
depends_on:
- US-TSDK-17
- US-TSDK-19
epic_id: EPIC-TSDK-6
id: US-TSDK-20
points: 3
priority: could
status: ready
tags:
- step-6
- quality
title: Adopt C# 14 syntax, records and OrderedDictionary
updated: '2026-09-18'
---

As a maintainer, I want the code to use params ReadOnlySpan<T>, the field keyword, extension members, sealed records for value types, required members on SystemOneRequest and OrderedDictionary for Answers so that the port reads as idiomatic modern C# and answer order is guaranteed.