---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-1-5
id: US-TSDK-1-7
points: 1
status: done
story_id: US-TSDK-1
tags: []
title: Document the net10.0 requirement in README, CLAUDE.md and .project/PROJECT.md
updated: '2026-09-18'
---

Update README.md (requirements/install section), CLAUDE.md (Stack line: remove the RollForward note) and .project/PROJECT.md (Tech Stack) to state that the SDK targets net10.0 and requires the .NET 10 SDK/runtime. Mention in the README Differences or Compatibility section that .NET 8 consumers must stay on 0.6.0. Acceptance: all three docs name net10.0; no mention of RollForward remains.