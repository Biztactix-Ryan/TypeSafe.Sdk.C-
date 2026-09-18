---
acceptance_criteria:
- NoulQuestion, ChoiceQuestion and ScoreQuestion derive from Question<NoulAnswer>,
  Question<ChoiceAnswer> and Question<ScoreAnswer>
- Question.Noul("billing", ...) returns Named<NoulAnswer> and Named<T> implements
  a non-generic INamedQuestion
- The dictionary-based overload still works for callers porting from Python
- 'The Questions : Dictionary class is removed'
created: '2026-09-18'
depends_on: []
epic_id: EPIC-TSDK-4
id: US-TSDK-11
points: 5
priority: must
status: done
tags:
- step-4
- generics
- breaking
title: Add Question<TAnswer> and name-first builders returning Named<T>
updated: '2026-09-18'
---

As a caller, I want each question type to declare the answer type it produces and to carry its wire name so that the question object itself can be the key I read the answer with.