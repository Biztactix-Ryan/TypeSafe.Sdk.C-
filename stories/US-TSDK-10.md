---
acceptance_criteria:
- An answer with an undeclared type deserializes to the base Answer and is excluded
  from Nouls, Choices and Scores
- A warning is logged naming the question; whether the discriminator value survives
  FallBackToBaseType is confirmed and documented
- The raw payload of the unknown answer remains reachable through RawHttpResponse
  or the extension data
created: '2026-09-18'
depends_on:
- US-TSDK-8
epic_id: EPIC-TSDK-3
id: US-TSDK-10
points: 2
priority: should
status: done
tags:
- step-3
- forward-compat
title: Preserve the unknown-answer-type warning
updated: '2026-09-18'
---

As a consumer on an older SDK, I want unknown answer types to be skipped with a warning naming the question and, if possible, the type so that forward compatibility matches the Python SDK after the parser rewrite.