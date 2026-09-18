---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-1-5
id: US-TSDK-3-7
points: 2
status: done
story_id: US-TSDK-3
tags: []
title: Replace int Status with HttpStatusCode StatusCode on ApiResponse and TypeSafeApiException
updated: '2026-09-18'
---

In src/TypeSafe.Sdk/Responses.cs (ApiResponse.Status, line ~14) and src/TypeSafe.Sdk/Exceptions.cs (TypeSafeApiException.Status, line ~53) expose `System.Net.HttpStatusCode StatusCode` and remove the int property. Update Transport.cs where the exception factory maps status codes, ErrorTests/ResponseTests, and README error table. Acceptance: no public `int Status` remains; tests pass.