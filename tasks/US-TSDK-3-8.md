---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-1-5
id: US-TSDK-3-8
points: 2
status: done
story_id: US-TSDK-3
tags: []
title: Make ListModelsResponse an IReadOnlyList<ModelMetadata> and add Probability/Label
  answer aliases
updated: '2026-09-18'
---

In src/TypeSafe.Sdk/Responses.cs: have ListModelsResponse (line ~238) implement IReadOnlyList<ModelMetadata> (Count, indexer, enumerator) so callers iterate the response directly; keep the existing Models property only if the README relies on it, otherwise remove. Add `NoulAnswer.Probability` as an alias of Noul and `ChoiceAnswer.Label` as an alias of Choice (plain get-only properties, not serialized). Update ModelsResource XML docs, tests and README. Acceptance: `foreach (var m in await client.Models.ListAsync())` compiles; aliases return the same values.