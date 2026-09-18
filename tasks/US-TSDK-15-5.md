---
archived: false
assignee: null
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-14-5
- US-TSDK-12-6
id: US-TSDK-15-5
points: 3
status: todo
story_id: US-TSDK-15
tags: []
title: Add ScoreQuestion<TEnum>, ScoreAnswer<TEnum> with Nearest and Question.Score<TEnum>
updated: '2026-09-18'
---

Add a builder `Question.Score<TEnum>(string name, Content instructions = default)` returning Named<ScoreAnswer<TEnum>>: validate that the enum's underlying values are exactly 0..N-1 (otherwise TypeSafeException `Enum Urgency must have contiguous values starting at 0 to be used as a score rubric.`), and serialize a plain score question whose criteria are the members' Description text in value order (falling back to the label). Add `public sealed record ScoreAnswer<TEnum>(double Score, double Confidence, IReadOnlyDictionary<TEnum, JsonNode?> Legend, IReadOnlyDictionary<TEnum, double> Probabilities) : Answer` with `TEnum Nearest` = the member whose value equals Math.Round(Score, MidpointRounding.AwayFromZero) clamped to the range. Extend SystemOneResponse.Get to convert the wire ScoreAnswer (int keys -> enum members; an out-of-range key throws TypeSafeApiResponseValidationException at `answers.<name>.legend` or `.probabilities`). The untyped ScoreAnswer path stays. Tests cover rejection of non-contiguous enums, rubric JSON in value order, typed conversion and Nearest rounding at boundaries. Acceptance: zero-warning build; tests pass.