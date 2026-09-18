---
acceptance_criteria:
- TypeSafeApiConnectionException has a nullable HttpRequestError Error property populated
  from the inner HttpRequestException
- A test asserts NameResolutionError is surfaced when the stub throws an HttpRequestException
  with that error
- README error table mentions the property
created: '2026-09-18'
depends_on: []
epic_id: EPIC-TSDK-6
id: US-TSDK-18
points: 2
priority: should
status: ready
tags:
- step-6
title: Expose HttpRequestError on connection exceptions
updated: '2026-09-18'
---

As a caller, I want TypeSafeApiConnectionException to carry HttpRequestException.HttpRequestError so that I and the retry predicate can distinguish DNS failures from resets and TLS errors.