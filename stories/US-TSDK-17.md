---
acceptance_criteria:
- TypeSafeClientOptions exposes TimeProvider defaulting to TimeProvider.System
- Backoff uses Task.Delay(delay, timeProvider, cancellationToken) and RetryAfterParser
  uses timeProvider.GetUtcNow()
- RetryTests assert the 500ms, 1s, 2s schedule and Retry-After HTTP dates using Microsoft.Extensions.TimeProvider.Testing
  without real sleeps
created: '2026-09-18'
depends_on: []
epic_id: EPIC-TSDK-6
id: US-TSDK-17
points: 3
priority: should
status: done
tags:
- step-6
- tests
title: Inject TimeProvider for retry delays and Retry-After maths
updated: '2026-09-18'
---

As a maintainer, I want retry sleeps and Retry-After date parsing to go through an injectable TimeProvider so that tests assert the real backoff schedule with FakeTimeProvider instead of a 1ms policy.