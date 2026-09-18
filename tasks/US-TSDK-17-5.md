---
archived: false
assignee: null
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-17-4
id: US-TSDK-17-5
points: 1
status: todo
story_id: US-TSDK-17
tags: []
title: Assert the real backoff schedule and Retry-After dates with FakeTimeProvider
updated: '2026-09-18'
---

Add PackageReference Microsoft.Extensions.TimeProvider.Testing (exact current stable, net10.0-compatible) to tests/TypeSafe.Sdk.Tests. Rewrite the RetryTests that currently rely on a 1ms policy so they use the DEFAULT RetryPolicy with a FakeTimeProvider: start the SystemOneAsync task, then advance the fake clock (FakeTimeProvider.Advance) in a loop until the request completes, recording each delay; assert the 500ms, 1s, 2s schedule (with jitter accounted for per RetryPolicy's implementation, or with jitter disabled if the policy allows) and that no real sleep occurred (wall-clock elapsed well under the summed delays). Add a Retry-After HTTP-date test: server returns `Retry-After: <RFC1123 date 30s after the fake now>` and the observed delay is 30s of fake time. Keep the fast-policy tests that cover other behaviour. Acceptance: RetryTests assert the 500ms/1s/2s schedule and a Retry-After date without real sleeps; whole suite still runs in about the same wall time; zero-warning build.