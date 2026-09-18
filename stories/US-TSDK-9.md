---
acceptance_criteria:
- Instructions and NoulCriteria are omitted when null via JsonIgnore WhenWritingNull
- RawQuestion passes its JsonObject through unchanged after the existing validation
- All QuestionTests expected JSON strings pass unchanged
- Validation errors (empty questions, empty score criteria, missing type) keep their
  current messages
created: '2026-09-18'
depends_on:
- US-TSDK-5
- US-TSDK-7
epic_id: EPIC-TSDK-3
id: US-TSDK-9
points: 3
priority: must
status: done
tags:
- step-3
- json
title: Serialize questions through the context
updated: '2026-09-18'
---

As an SDK author, I want Question records serialized by the same JsonSerializerContext so that the hand-written ToJson methods go away and question serialization is AOT safe.