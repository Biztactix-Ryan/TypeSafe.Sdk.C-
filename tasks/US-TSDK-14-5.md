---
archived: false
assignee: null
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on: []
id: US-TSDK-14-5
points: 2
status: todo
story_id: US-TSDK-14
tags: []
title: Add the trim-safe enum label and description helper
updated: '2026-09-18'
---

Create src/TypeSafe.Sdk/Internal/EnumLabels.cs: for `TEnum : struct, Enum`, produce an ordered list of (member, label, description?) where label comes from [JsonStringEnumMemberName] when present, otherwise JsonNamingPolicy.SnakeCaseLower.ConvertName(member name), and description from [System.ComponentModel.Description] when present. Use Enum.GetValues<TEnum>()/Enum.GetNames<TEnum>() and typeof(TEnum).GetField(name) with a [DynamicallyAccessedMembers(PublicFields)] annotation on the generic parameter (or an equivalent that keeps the IsAotCompatible build warning-free); cache per TEnum in a static generic class. Also expose reverse lookup label -> member. Unit tests: attribute label wins, snake_case fallback (`VeryUrgent` -> `very_urgent`), descriptions read in declaration order, reverse lookup of an unknown label returns false. Acceptance: zero-warning build with the AOT analyzers on; tests pass.