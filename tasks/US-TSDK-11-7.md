---
archived: false
assignee: null
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-11-6
id: US-TSDK-11-7
points: 1
status: todo
story_id: US-TSDK-11
tags: []
title: Remove the Questions dictionary class and update callers
updated: '2026-09-18'
---

Delete `public sealed class Questions : Dictionary<string, Question>` from Questions.cs. Callers pass any IEnumerable<KeyValuePair<string, Question>> (a Dictionary<string, Question> or a collection expression) to the dictionary-based SystemOneAsync overload, which stays for callers porting from Python. Update README samples, the demo and tests; add a README Differences bullet and a DECISIONS.md line for the removal. Acceptance: no reference to the Questions class remains in src/, tests/, examples/ or README; build zero warnings; tests pass.