---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on: []
id: US-TSDK-24-5
points: 2
status: done
story_id: US-TSDK-24
tags: []
title: Add the key-gated live integration test project
updated: '2026-09-18'
---

Create tests/TypeSafe.Sdk.IntegrationTests (net10.0, xunit 2.9.3 matching the unit project, referencing src/TypeSafe.Sdk) and add it to TypeSafe.Sdk.slnx. Gate every test on TYPESAFE_API_KEY: add Xunit.SkippableFact (exact current stable) and use [SkippableFact] with `Skip.If(string.IsNullOrEmpty(key), "TYPESAFE_API_KEY is not set; live tests skipped")` so the reason is visible in test output. Tests: Models.ListAsync returns at least one model with a non-empty name; SystemOneAsync with a noul, a choice and a score question (named builders) returns all three answers with choice/score probabilities summing to 1 within 0.01 and noul in [0,1]; a client built with a deliberately bad key gets TypeSafeAuthenticationException whose RequestId is non-empty. Honour TYPESAFE_BASE_URL if set. Never print the key. Also run the project in ci.yml (`dotnet test` at the solution root already includes it; confirm) so CI shows the skips; do NOT add the secret to CI. Acceptance: `dotnet test` without the key shows the integration tests as skipped with the reason and the unit suite unchanged; with a key the tests are written to pass (state clearly that this cannot be verified on this machine); zero-warning build.