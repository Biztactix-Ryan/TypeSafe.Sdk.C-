---
archived: false
assignee: null
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on: []
id: US-TSDK-18-4
points: 2
status: todo
story_id: US-TSDK-18
tags: []
title: Carry HttpRequestError on TypeSafeApiConnectionException
updated: '2026-09-18'
---

In src/TypeSafe.Sdk/Exceptions.cs add `public HttpRequestError? Error { get; }` to TypeSafeApiConnectionException (constructor parameter, default null; TypeSafeApiTimeoutException inherits it and passes null). In Internal/Transport.cs, where HttpRequestException/IOException are mapped to the connection exception, populate Error from HttpRequestException.HttpRequestError (null for IOException). Keep messages unchanged. Add tests: a StubHandler that throws HttpRequestException with HttpRequestError.NameResolutionError surfaces Error == NameResolutionError; an IOException surfaces null; RetryWhen can read it. Update the README error table row for TypeSafeApiConnectionException to mention Error. Acceptance: build zero warnings; tests pass; README updated.