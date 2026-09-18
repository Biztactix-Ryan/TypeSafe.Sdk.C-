---
archived: false
assignee: null
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-20-5
id: US-TSDK-20-6
points: 2
status: todo
story_id: US-TSDK-20
tags: []
title: Preserve wire order with an OrderedDictionary Answers and expose Nouls/Choices/Scores
  as extension members
updated: '2026-09-18'
---

Change SystemOneResponse.Answers (src/TypeSafe.Sdk/Responses.cs) to `System.Collections.Generic.OrderedDictionary<string, Answer>` exposed read-only (e.g. the property type `OrderedDictionary<string, Answer>` returned as a read-only wrapper, or keep `IReadOnlyDictionary<string, Answer>` on the interface and add `IReadOnlyList<KeyValuePair<string, Answer>>`-style index access; the AC requires wire order preserved and index access: `response.Answers.GetAt(0)` or an indexer by position). AnswerMapConverter must build it in wire order (it already reads sequentially). Add C# 14 extension members (`extension(IReadOnlyDictionary<string, Answer> answers) { public IReadOnlyDictionary<string, NoulAnswer> Nouls ... Choices ... Scores }`) in a static class so any answer dictionary gets the typed views; keep `response.Nouls/Choices/Scores` working as instance members that forward to the extension (or remove them only if every caller, README and demo are updated and the divergence is recorded). Tests: a body whose answers arrive in a non-alphabetical order yields the same order from Answers and index access; the extension views work on a plain dictionary. Re-approve the API snapshot. Acceptance: wire order preserved with index access; extension members exist; existing ResponseTests/EnumTests pass; zero-warning build (OrderedDictionary<TKey,TValue> is in-box on net10.0; ensure the source generator/converter still has no reflection).