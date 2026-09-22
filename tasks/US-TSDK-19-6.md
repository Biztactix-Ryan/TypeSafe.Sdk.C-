---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-19-5
id: US-TSDK-19-6
points: 1
status: done
story_id: US-TSDK-19
tags: []
title: Capture structured state in CapturingLogger and assert RequestId/StatusCode
  properties
updated: '2026-09-18'
---

Extend tests/TypeSafe.Sdk.Tests/TestSupport.cs CapturingLogger to keep each entry's EventId and its state as IReadOnlyList<KeyValuePair<string, object?>> (cast TState to IReadOnlyList<KeyValuePair<string, object?>> as LoggerMessage-generated state implements it). Add a LoggingTests test that performs a stubbed SystemOne call at Information level and asserts the response-received event carries named properties RequestId, StatusCode, Attempt and ElapsedMs with the expected values and its EventId matches the documented table; add one asserting the retry-scheduled event's Attempt/Delay. Acceptance: tests pass; existing message-based assertions keep passing.