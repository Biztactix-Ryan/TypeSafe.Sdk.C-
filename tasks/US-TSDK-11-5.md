---
archived: false
assignee: null
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on: []
id: US-TSDK-11-5
points: 3
status: todo
story_id: US-TSDK-11
tags: []
title: Introduce Question<TAnswer> and derive the three question records from it
updated: '2026-09-18'
---

In src/TypeSafe.Sdk/Questions.cs add `public abstract record Question<TAnswer> : Question where TAnswer : Answer` (no new serialized members) and change NoulQuestion, ChoiceQuestion and ScoreQuestion to derive from Question<NoulAnswer>, Question<ChoiceAnswer> and Question<ScoreAnswer>. RawQuestion stays on the non-generic Question. Keep the [JsonPolymorphic]/[JsonDerivedType] attributes on the non-generic base and the TypeSafeJsonContext registrations working (the generic intermediate must not break the source generator; if it does, register the closed generic types or keep the attributes on the closed records). Acceptance: build has zero warnings; every existing QuestionTests/RequestTests expected JSON string passes unchanged; `Question<NoulAnswer> q = Question.Noul("x")` compiles.