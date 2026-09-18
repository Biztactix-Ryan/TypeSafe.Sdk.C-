---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-11-7
- US-TSDK-12-5
- US-TSDK-14-6
- US-TSDK-15-5
id: US-TSDK-23-4
points: 2
status: done
story_id: US-TSDK-23
tags: []
title: Add the PublicApiGenerator + Verify snapshot test and CONTRIBUTING notes
updated: '2026-09-18'
---

In tests/TypeSafe.Sdk.Tests add PackageReferences PublicApiGenerator and Verify.Xunit (pin exact versions; confirm they build on net10.0 with zero warnings). Add PublicApiTests.cs: generate the public API of typeof(Content).Assembly with PublicApiGenerator (exclude assembly attributes that vary, e.g. version/InternalsVisibleTo if they churn) and `Verify` it against a checked-in `PublicApiTests.PublicApi.verified.txt` next to the test; run once to create the approved file and commit it (leave it in the working tree). Add CONTRIBUTING.md at the repo root explaining: the snapshot fails on any public signature change, how to review the .received.txt diff and accept it by replacing the .verified.txt, and that breaking changes need a README Differences entry and a DECISIONS.md line. Mutation-check once (add a public member, see the test fail, revert byte-identically). Acceptance: test passes on the current tree; the verified file is present; CONTRIBUTING explains acceptance; zero-warning build.