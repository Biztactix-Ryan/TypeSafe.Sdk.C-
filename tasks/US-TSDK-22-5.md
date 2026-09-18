---
archived: false
assignee: null
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on: []
id: US-TSDK-22-5
points: 1
status: todo
story_id: US-TSDK-22
tags: []
title: 'Complete package metadata: README, license, symbols, SourceLink'
updated: '2026-09-18'
---

In src/TypeSafe.Sdk/TypeSafe.Sdk.csproj set PackageReadmeFile (README.md included as None with Pack=true), PackageLicenseExpression (match the repo LICENSE; if no LICENSE file exists, stop and raise it), PackageProjectUrl/RepositoryUrl/RepositoryType, PublishRepositoryUrl=true, EmbedUntrackedSources=true, IncludeSymbols=true with SymbolPackageFormat=snupkg, Deterministic (already on), and confirm the .NET 10 SDK's built-in SourceLink is active (no extra package needed; add Microsoft.SourceLink.GitHub only if the nupkg lacks sourcelink.json). Verify by running `dotnet pack -c Release -o artifacts` and inspecting the nupkg (unzip -l) for README.md, the license expression in the nuspec, and the .snupkg. Acceptance: pack succeeds with zero warnings (NU5xxx included) and the inspection shows all four items.