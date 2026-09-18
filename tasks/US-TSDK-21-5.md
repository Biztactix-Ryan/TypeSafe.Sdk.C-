---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-21-4
id: US-TSDK-21-5
points: 2
status: done
story_id: US-TSDK-21
tags: []
title: Add the GitHub Actions CI workflow
updated: '2026-09-18'
---

Create .github/workflows/ci.yml triggered on push to main and pull_request: ubuntu-latest, actions/checkout, actions/setup-dotnet with dotnet-version 10.0.x, `dotnet restore`, `dotnet build --no-restore -warnaserror`, `dotnet test --no-build`, `scripts/check-version.sh`, `dotnet pack src/TypeSafe.Sdk/TypeSafe.Sdk.csproj -c Release --no-build -o artifacts`, and upload the nupkg as an artifact. Validate the YAML locally (e.g. a YAML parse plus `act` dry run if available; otherwise document that the first run on GitHub is the proof). Record the CI design in .project/INFRASTRUCTURE.md (Build Pipeline section). Acceptance: the workflow file exists, is valid YAML, runs the four steps with warnings as errors, and calls the version check.