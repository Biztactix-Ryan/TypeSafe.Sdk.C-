---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-8-6
id: US-TSDK-10-4
points: 2
status: done
story_id: US-TSDK-10
tags: []
title: Detect base-type Answer fallbacks, log a warning naming the question, and exclude
  them from the typed views
updated: '2026-09-18'
---

After deserialization in SystemOneResponse (src/TypeSafe.Sdk/Responses.cs), detect answers whose runtime type is exactly `Answer` (FallBackToBaseType hit). Log a warning through the transport logger naming the question and, if available, the unrecognized type: confirm experimentally whether the `type` discriminator survives into the [JsonExtensionData] dictionary under FallBackToBaseType and record the finding in .project/DECISIONS.md and the XML docs. Exclude such answers from Nouls, Choices and Scores while keeping them in Answers; the raw payload stays reachable through the extension data and RawHttpResponse. Mirrors the current `Ignoring answer "{name}" with unrecognized type` warning (old Responses.cs ~189). Acceptance: LoggingTests sees the warning; the unknown answer is absent from the typed views.