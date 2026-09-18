---
created: '2026-09-18'
id: EPIC-TSDK-6
points: null
priority: should
status: draft
tags:
- typing-review
- step-6
- quality
target_date: null
title: 'Runtime and language cleanups: TimeProvider, HttpRequestError, LoggerMessage,
  C# 14 syntax'
updated: '2026-09-18'
---

Step 6 of the typing review, a bundle of small non-breaking improvements: inject TimeProvider (default TimeProvider.System) for retry delays and Retry-After date maths so tests use FakeTimeProvider and assert the real 500ms/1s/2s schedule; expose HttpRequestException.HttpRequestError as TypeSafeApiConnectionException.Error; replace SdkLog string formatting with [LoggerMessage] partial methods carrying RequestId/StatusCode/Attempt/ElapsedMs and EventIds; use OrderedDictionary<string, Answer> for Answers; adopt params ReadOnlySpan<T> on builders, the `field` keyword for lazy properties, extension members for the Nouls/Choices/Scores grouping helpers, sealed records for Usage/ModelMetadata/RequestOptions/TypeSafeClientOptions, and `required` on SystemOneRequest.

Success: retry tests no longer rely on 1ms backoff; structured log fields visible in a CapturingLogger test; no behaviour change for callers.