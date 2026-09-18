---
created: '2026-09-18'
id: EPIC-TSDK-4
points: null
priority: must
status: draft
tags:
- typing-review
- step-4
- breaking
- generics
target_date: null
title: 'Typed questions: Question<TAnswer>, Named<T>, response.Get(question)'
updated: '2026-09-18'
---

Step 4 of the typing review and the biggest win. Add a generic layer `Question<TAnswer> : Question` so each question type names the answer it produces; builders take the wire name first and return `Named<TAnswer>`; `SystemOneAsync(Content state, params ReadOnlySpan<INamedQuestion> questions, ...)` accepts collection expressions; `SystemOneResponse.Get(Named<T>)` returns the typed answer and throws a precise TypeSafeException when the name is missing or the type differs. Keep Answers/Nouls/Choices/Scores for Python-parity callers; delete the `Questions : Dictionary` class.

Success: the demo uses the typed path end to end; a wrong-type Get produces a message naming both types; README quickstart rewritten around named questions; old dictionary overload still available. Breaking change.