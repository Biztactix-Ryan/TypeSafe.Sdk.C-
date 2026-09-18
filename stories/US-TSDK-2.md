---
acceptance_criteria:
- TypeSafe.Sdk.csproj sets IsAotCompatible to true
- The only IL2026/IL3050 sites are the reflection-based object conversion and they
  carry RequiresUnreferencedCode and RequiresDynamicCode attributes
- dotnet publish of the demo with PublishAot=true completes with warnings limited
  to the annotated overload
created: '2026-09-18'
depends_on: []
epic_id: EPIC-TSDK-1
id: US-TSDK-2
points: 3
priority: must
status: backlog
tags:
- step-1
- aot
title: Enable IsAotCompatible and clear analyzer warnings
updated: '2026-09-18'
---

As a consumer publishing with PublishAot or PublishTrimmed, I want the SDK to carry the AOT-compatible marker and produce no trim warnings so that it runs in trimmed Azure Functions and container images.