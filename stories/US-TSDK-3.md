---
acceptance_criteria:
- RetryPolicy exposes RetryConnectionErrors, RetryTimeouts and RetryWhen; the old
  names are gone
- TypeSafeApiException and ApiResponse expose StatusCode as HttpStatusCode only; the
  int Status property is removed
- ListModelsResponse implements IReadOnlyList<ModelMetadata>
- NoulAnswer.Probability and ChoiceAnswer.Label exist as aliases of Noul and Choice
- TypeSafeClientOptions uses init setters and README examples still compile
created: '2026-09-18'
depends_on:
- US-TSDK-1
epic_id: EPIC-TSDK-1
id: US-TSDK-3
points: 5
priority: must
status: done
tags:
- step-1
- breaking
title: Apply the naming table to the public surface
updated: '2026-09-18'
---

As a .NET developer, I want SDK members named by .NET conventions so that RetryPolicy flags read as actions, status codes use HttpStatusCode, and the models list doesn't require models.Models.