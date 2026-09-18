---
archived: false
assignee: null
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-24-5
id: US-TSDK-24-6
points: 1
status: todo
story_id: US-TSDK-24
tags: []
title: Document the live suite and update the SECURITY.md stub-only-testing risk
updated: '2026-09-18'
---

In .project/SECURITY.md update the risk that says testing is stub-only: describe the gated live suite (tests/TypeSafe.Sdk.IntegrationTests, TYPESAFE_API_KEY, skip behaviour), and mark the risk as mitigated once the suite has passed against the live API (record explicitly that as of this change it has not yet been run with a key). Add a CLAUDE.md Commands row `Live tests | TYPESAFE_API_KEY=... dotnet test tests/TypeSafe.Sdk.IntegrationTests` and a README Development note. Acceptance: SECURITY.md, CLAUDE.md and README updated consistently; nothing else changes.