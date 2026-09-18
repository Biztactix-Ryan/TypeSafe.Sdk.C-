---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-8-5
id: US-TSDK-8-6
points: 2
status: done
story_id: US-TSDK-8
tags: []
title: Delete Wire, ResponseFieldException and the static Parse methods; reconcile
  ResponseTests
updated: '2026-09-18'
---

Remove SystemOneResponse.Parse (Responses.cs ~172), ListModelsResponse.Parse (~250), ResponseFieldException (~269) and the Wire class (~280 onward). SystemOneResponse and ListModelsResponse become thin wrappers constructed from the deserialized body records plus the HttpResponseMessage. Update every malformed-body test in tests/TypeSafe.Sdk.Tests/ResponseTests.cs to the `$.`-stripped FieldPath the context produces, keeping each test's intent. Record `wc -l src/TypeSafe.Sdk/**/*.cs` before and after in the task note. Acceptance: no references to Wire/ResponseFieldException/Parse remain; library line count drops by 150+; all tests pass.