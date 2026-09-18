---
acceptance_criteria:
- Tests are skipped with a clear reason when TYPESAFE_API_KEY is absent
- Models.ListAsync returns at least one model and SystemOneAsync answers a noul, choice
  and score question with probabilities summing to approximately one
- A 401 from a bad key surfaces as TypeSafeAuthenticationException with a request
  id
- SECURITY.md risk about stub-only testing is updated once the suite passes
created: '2026-09-18'
depends_on: []
epic_id: EPIC-TSDK-7
id: US-TSDK-24
points: 3
priority: should
status: ready
tags:
- integration
- tests
title: Live integration tests gated on TYPESAFE_API_KEY
updated: '2026-09-18'
---

As the maintainer, I want an integration test project that hits the real API when a key is present so that the port is validated against the live wire format once access is granted.