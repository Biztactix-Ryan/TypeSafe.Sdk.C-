---
acceptance_criteria:
- All transport log calls go through [LoggerMessage] partial methods with EventIds
- A CapturingLogger test sees RequestId and StatusCode as named state properties on
  the response-received event
- Header redaction and body logging at Debug behave as before
- SdkLog is reduced to the minimum-level filter or removed
created: '2026-09-18'
depends_on: []
epic_id: EPIC-TSDK-6
id: US-TSDK-19
points: 3
priority: should
status: backlog
tags:
- step-6
- logging
title: Structured logging with LoggerMessage source generation
updated: '2026-09-18'
---

As an operator using a structured log sink, I want SDK log events to carry RequestId, StatusCode, Attempt and ElapsedMs as properties with stable EventIds so that I can query them rather than parse message text.