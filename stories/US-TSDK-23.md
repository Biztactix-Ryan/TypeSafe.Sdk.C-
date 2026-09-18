---
acceptance_criteria:
- A test using PublicApiGenerator and Verify writes the approved public API to a checked-in
  file
- Changing a public signature fails the test until the snapshot is re-approved
- CONTRIBUTING notes explain how to accept a snapshot change
created: '2026-09-18'
depends_on:
- US-TSDK-11
- US-TSDK-12
- US-TSDK-14
- US-TSDK-15
epic_id: EPIC-TSDK-7
id: US-TSDK-23
points: 2
priority: should
status: done
tags:
- tests
- api
title: Public API snapshot test
updated: '2026-09-18'
---

As a maintainer, I want the public API surface snapshotted in a test so that breaking changes are deliberate and reviewed, mirroring the Python SDK's public surface snapshot.