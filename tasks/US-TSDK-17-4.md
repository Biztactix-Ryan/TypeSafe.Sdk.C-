---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on: []
id: US-TSDK-17-4
points: 2
status: done
story_id: US-TSDK-17
tags: []
title: Thread an injectable TimeProvider through options, backoff and Retry-After
updated: '2026-09-18'
---

Add `public TimeProvider TimeProvider { get; init; } = TimeProvider.System;` to src/TypeSafe.Sdk/TypeSafeClientOptions.cs (XML-doc: used for retry sleeps and Retry-After maths). Pass it into Internal/Transport.cs and replace `Task.Delay(delay, cancellationToken)` (line ~275) with `Task.Delay(delay, timeProvider, cancellationToken)`; make Internal/RetryAfterParser.cs take the current time from `timeProvider.GetUtcNow()` instead of `DateTimeOffset.UtcNow` (keep the existing `now` parameter shape if tests use it, sourcing the default from the provider). Elapsed-ms logging may use timeProvider.GetTimestamp()/GetElapsedTime. Extend the tests' ClientSetup helper (TestSupport.cs) with a TimeProvider field so RetryTests can inject one. Behaviour with the default provider is unchanged: all existing tests pass. Acceptance: option exists with the System default; no DateTimeOffset.UtcNow or bare Task.Delay remains in src/ for retry timing; zero-warning build (AOT analyzers on); tests pass.