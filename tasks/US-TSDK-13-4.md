---
archived: false
assignee: null
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-12-5
- US-TSDK-12-6
id: US-TSDK-13-4
points: 2
status: todo
story_id: US-TSDK-13
tags: []
title: Rewrite the README quickstart and the demo around Named questions and Get
updated: '2026-09-18'
---

README.md: make the quickstart declare questions with the name-first builders (`var billing = Question.Noul("billing", ...)`, `var tone = Question.Choice("tone", ...)`), call `client.SystemOneAsync(state, [billing, tone])` and read `response.Get(tone).Choice`; keep the dictionary form as a short 'porting from Python' note; add a Differences bullet explaining that the C# SDK gets answer types from the question object (Named<TAnswer>) where the JavaScript SDK infers them from the literal question map. examples/TypeSafe.Sdk.Demo/Program.cs: use named questions and Get for every answer it prints. Compile every README csharp block via the usual temporary scratch file in the test project (then delete it). Acceptance: demo builds and uses Get for each answer; README quickstart uses Named questions and Get; Differences bullet present; zero-warning build; tests pass.