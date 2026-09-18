---
archived: false
assignee: null
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-11-6
id: US-TSDK-12-6
points: 2
status: todo
story_id: US-TSDK-12
tags: []
title: Add SystemOneResponse.Get<TAnswer>(Named<TAnswer>) with precise failures
updated: '2026-09-18'
---

In src/TypeSafe.Sdk/Responses.cs add `public TAnswer Get<TAnswer>(Named<TAnswer> question) where TAnswer : Answer` (and a `TryGet` returning bool with an out parameter). Lookup by question.Name in Answers: missing name throws TypeSafeException `No answer named "tone" in the response.`; present but wrong runtime type throws TypeSafeException naming both types, e.g. `Answer "tone" is a NoulAnswer, not a ChoiceAnswer.`; a bare Answer (unknown type) reports its extension-data type string. Answers, Nouls, Choices and Scores stay as they are. Unit tests cover the hit, the missing name, the type mismatch and the unknown-type case. Acceptance: tests pass; zero-warning build; XML docs on the new members.