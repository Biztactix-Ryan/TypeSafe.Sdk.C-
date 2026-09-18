---
acceptance_criteria:
- Responses.Wire, ResponseFieldException and the static Parse methods no longer exist
- Every malformed-body test in ResponseTests passes with its expected FieldPath, reconciled
  to the $.-stripped JsonException.Path
- Validation exceptions still carry status, body, headers, request id and endpoint
- Library line count drops by at least 150 lines
created: '2026-09-18'
depends_on: []
epic_id: EPIC-TSDK-3
id: US-TSDK-8
points: 5
priority: must
status: backlog
tags:
- step-3
- json
title: Route transport parsing through the context and delete Wire
updated: '2026-09-18'
---

As a maintainer, I want Transport to deserialize with the context and translate JsonException.Path into TypeSafeApiResponseValidationException.FieldPath so that the Wire helpers, hand Parse methods and ResponseFieldException can be deleted.