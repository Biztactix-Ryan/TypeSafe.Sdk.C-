---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-4-6
- US-TSDK-3-9
id: US-TSDK-5-5
points: 3
status: done
story_id: US-TSDK-5
tags: []
title: Switch Question builders and NoulCriteria to Content parameters and remove
  the TDescription overloads
updated: '2026-09-18'
---

In src/TypeSafe.Sdk/Questions.cs change Question.Noul(instructions, whenTrue, whenFalse), Question.Choice(instructions, params string[] labels), the dictionary Choice overload, Question.Score(instructions, params criteria) and the NoulCriteria constructor to take `Content` instead of `object?`. Delete the generic `Choice<TDescription>` and `Score<TDescription>` overloads (lines ~61 and ~87). Score criteria become `params Content[]` (or ReadOnlySpan<Content>) so `Question.Score("How urgent?", ["can wait", "today"])` still compiles via the string conversion. Note: the acceptance criterion quotes the name-first form which arrives in US-TSDK-11; verify the positional form here. Update QuestionTests without changing expected JSON. Acceptance: no builder takes object?; QuestionTests expected JSON unchanged.