---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-14-6
- US-TSDK-15-5
- US-TSDK-13-4
id: US-TSDK-16-3
points: 2
status: done
story_id: US-TSDK-16
tags: []
title: Showcase Tone and Urgency enums in the demo and add a README Enums section
updated: '2026-09-18'
---

examples/TypeSafe.Sdk.Demo/Program.cs: declare `enum Tone { Calm, Frustrated, [JsonStringEnumMemberName("very_angry")] VeryAngry }` and `enum Urgency { [Description("can wait")] Low = 0, [Description("today")] Medium = 1, [Description("right now")] High = 2 }`, ask `Question.Choice<Tone>("tone", ...)` and `Question.Score<Urgency>("urgency", ...)`, and print `Get(urgency).Nearest` plus the enum-keyed probabilities. README.md: add an Enums section after Questions showing both builders, the label rules (JsonStringEnumMemberName, else snake_case), Description as criteria/rubric text, the contiguous-from-zero rule for score enums, and the validation failure for undeclared labels; compile the README blocks via the usual temporary scratch file (then delete it). Acceptance: demo builds (and AOT-publishes with zero warnings) and prints Nearest and enum-keyed probabilities; README Enums section present and compiling; zero-warning build; tests pass.