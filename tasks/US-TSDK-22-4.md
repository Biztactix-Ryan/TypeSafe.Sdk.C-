---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-21-5
id: US-TSDK-22-4
points: 2
status: done
story_id: US-TSDK-22
tags: []
title: Add the tag-triggered NuGet release workflow
updated: '2026-09-18'
---

Create .github/workflows/release.yml triggered by tags matching v*: checkout, setup-dotnet 10.0.x, derive VERSION from the tag (strip the v), run scripts/check-version.sh against that VERSION (extend the script to accept an expected version argument), `dotnet build -c Release -warnaserror`, `dotnet test -c Release --no-build`, `dotnet pack src/TypeSafe.Sdk/TypeSafe.Sdk.csproj -c Release --no-build -o artifacts`, then `dotnet nuget push artifacts/*.nupkg --source https://api.nuget.org/v3/index.json --api-key ${{ secrets.NUGET_API_KEY }} --skip-duplicate` (symbols pushed alongside). Validate the YAML. Acceptance: workflow file valid and referencing the NUGET_API_KEY secret; the secret itself must be created by a human in the GitHub repo settings, so raise that with pm_update(note=...) as a follow-up rather than blocking.