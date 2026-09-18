---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-6-5
- US-TSDK-8-6
- US-TSDK-9-6
id: US-TSDK-2-4
points: 3
status: done
story_id: US-TSDK-2
tags: []
title: Set IsAotCompatible and clear every trim/AOT analyzer warning outside the annotated
  overload
updated: '2026-09-18'
---

Add <IsAotCompatible>true</IsAotCompatible> to src/TypeSafe.Sdk/TypeSafe.Sdk.csproj (this turns on the trim, single-file and AOT analyzers). Fix every IL2026/IL2087/IL3050 warning except the ones originating from Content.From(object) / JsonContent.From, which are already annotated: expected sites are any remaining JsonSerializer.Serialize/Deserialize calls in Internal/JsonContent.cs and Internal/Transport.cs that do not use the TypeSafeJsonContext. Then run `dotnet publish examples/TypeSafe.Sdk.Demo -c Release -r linux-x64 -p:PublishAot=true` and confirm the only warnings come from the annotated overload (the demo should use the typed path, so ideally none). Acceptance: zero build warnings; AOT publish output recorded in the task note.