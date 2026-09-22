---
acceptance_criteria:
- A separate project TypeSafe.Sdk.DependencyInjection references Microsoft.Extensions.Http
  and Options and registers ITypeSafeClient
- Configuration binds from a TypeSafe section and environment variables with code
  overrides winning
- The named HttpClient is not disposed by the SDK and resilience handlers can be added
  by the caller
- README has a DI section and the core package's dependency list is unchanged
created: '2026-09-18'
depends_on:
- US-TSDK-20
epic_id: EPIC-TSDK-7
id: US-TSDK-25
points: 5
priority: could
status: done
tags:
- di
- package
title: TypeSafe.Sdk.DependencyInjection package
updated: '2026-09-18'
---

As an ASP.NET or worker-service developer, I want services.AddTypeSafeClient(...) with IHttpClientFactory and IOptions<TypeSafeClientOptions> so that the client is registered the way every other .NET SDK is, without adding dependencies to the core package.