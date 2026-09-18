---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-1-5
id: US-TSDK-3-6
points: 2
status: done
story_id: US-TSDK-3
tags: []
title: Rename RetryPolicy flags to RetryConnectionErrors, RetryTimeouts and RetryWhen
updated: '2026-09-18'
---

In src/TypeSafe.Sdk/RetryPolicy.cs rename the connection-error and timeout retry booleans to RetryConnectionErrors and RetryTimeouts, and the custom predicate to RetryWhen; remove the old names (no obsolete shims, this is the breaking-change release). Update src/TypeSafe.Sdk/Internal/Transport.cs call sites (ShouldRetry stays internal), tests/TypeSafe.Sdk.Tests/RetryTests.cs and the README retry section. Acceptance: old names gone from the public surface; RetryTests pass.