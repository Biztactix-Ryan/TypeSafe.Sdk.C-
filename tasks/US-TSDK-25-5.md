---
archived: false
assignee: null
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on:
- US-TSDK-20-5
- US-TSDK-20-7
id: US-TSDK-25-5
points: 3
status: todo
story_id: US-TSDK-25
tags: []
title: Create the TypeSafe.Sdk.DependencyInjection project with AddTypeSafeClient
updated: '2026-09-18'
---

Add src/TypeSafe.Sdk.DependencyInjection/TypeSafe.Sdk.DependencyInjection.csproj (net10.0, IsAotCompatible, nullable, same warning policy; PackageId TypeSafe.Sdk.DependencyInjection, Version 0.7.0 via the same two-place rule or by referencing the core version; metadata mirroring the core csproj) referencing the core project plus Microsoft.Extensions.Http and Microsoft.Extensions.Options.ConfigurationExtensions (pin 10.0.x matching Logging.Abstractions). Add it to TypeSafe.Sdk.slnx. Public API: `IHttpClientBuilder AddTypeSafeClient(this IServiceCollection services, Action<TypeSafeClientOptions>? configure = null)` and `AddTypeSafeClient(this IServiceCollection services, IConfiguration section, Action<TypeSafeClientOptions>? configure = null)`. Behaviour: bind IOptions<TypeSafeClientOptions> from the given section (or the "TypeSafe" section when a configuration root is passed) with init-only record binding, then apply `configure` so code overrides win; environment variables keep working because the client itself resolves TYPESAFE_* for unset values; register a named HttpClient ("TypeSafe") through IHttpClientFactory and construct TypeSafeClient with that HttpClient and DisposeHttpClient = false; register ITypeSafeClient (transient, resolved from the factory client) and TypeSafeClient; return the IHttpClientBuilder so callers can add resilience handlers. Tests in a new tests/TypeSafe.Sdk.DependencyInjection.Tests project (xunit 2.9.3): registration resolves ITypeSafeClient; configuration section + code override precedence (code wins); the HttpClient from the factory is not disposed when the client is disposed (spy handler); a handler added via the returned builder is invoked; AOT dry run of the DI project with the analyzers (it is IsAotCompatible) is warning-free. Acceptance: project builds with zero warnings, tests pass, core csproj dependency list unchanged.