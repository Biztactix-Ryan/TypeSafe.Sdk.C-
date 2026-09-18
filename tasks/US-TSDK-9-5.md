---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-7-6
- US-TSDK-5-5
id: US-TSDK-9-5
points: 2
status: done
story_id: US-TSDK-9
tags: []
title: Convert Question, NoulCriteria and the three question types to context-serialized
  records and remove ToJson
updated: '2026-09-18'
---

In src/TypeSafe.Sdk/Questions.cs make Question a polymorphic record ([JsonPolymorphic] on `type` with JsonDerivedType noul/choice/score), NoulCriteria a record, and NoulQuestion/ChoiceQuestion/ScoreQuestion records whose Instructions (Content/JsonNode) and NoulCriteria carry [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]. Register them in TypeSafeJsonContext. Delete the internal ToJson(string name) methods (~100, 146, 170, 194, 220). Keep the existing validation (empty questions, empty score criteria, missing type) with identical messages. Acceptance: QuestionTests expected JSON strings pass unchanged.