---
archived: false
assignee: null
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-11-5
id: US-TSDK-11-6
points: 3
status: todo
story_id: US-TSDK-11
tags: []
title: Add Named<TAnswer>, INamedQuestion and name-first builders
updated: '2026-09-18'
---

Add `public interface INamedQuestion { string Name { get; } Question Question { get; } }` and `public sealed record Named<TAnswer>(string Name, Question<TAnswer> Question) : INamedQuestion where TAnswer : Answer` (explicit non-generic Question property). Add name-first builders on Question: Noul(string name, Content instructions = default, Content whenTrue = default, Content whenFalse = default) → Named<NoulAnswer>; Choice(string name, Content instructions, params string[] labels) and Choice(string name, Content instructions, IEnumerable<KeyValuePair<string, Content>> criteria) → Named<ChoiceAnswer>; Score(string name, Content instructions, params Content[] criteria) → Named<ScoreAnswer>. Validate name is non-empty with the existing message style. The positional builders stay for the dictionary path; make sure overload resolution between Noul(Content...) and Noul(string name, ...) is unambiguous for `Question.Noul("Is it urgent?")` (a lone string must keep meaning instructions; if that is impossible without ambiguity, record the chosen rule in DECISIONS.md and the README). Acceptance: `Question.Score("urgency", "How urgent?", ["can wait", "today"])` compiles and serializes to the same JSON as the positional form; unit tests cover each builder and INamedQuestion.