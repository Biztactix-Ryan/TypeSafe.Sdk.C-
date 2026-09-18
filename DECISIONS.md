# TypeSafe.Sdk — Decisions

Newest first.

## Unmodelled answers stay in `Answers`, warned about, with their discriminator in the extension data
- **Date**: 2026-09-18
- **Status**: Accepted (story TSDK-10)
- **Context**: Story TSDK-8 rebuilt reading on `TypeSafeJsonContext` and added `AnswerMapConverter`, which carries an answer whose `type` this SDK version does not model as a bare `Answer`. `SystemOneResponse.FromBody` then warned and *dropped* it, mirroring the Python SDK's skip. Two questions were left open: whether `JsonUnknownDerivedTypeHandling.FallBackToBaseType` makes the converter unnecessary, and whether a dropped answer leaves the caller any way to see what the server sent.
- **Experimental findings**: (a) `FallBackToBaseType` is write-side only. Reading `{"type":"rank","order":["a","b"]}` through `TypeSafeJsonContext.Default.Answer` throws `System.Text.Json.JsonException: Read unrecognized type discriminator id 'rank'. Path: $ | LineNumber: 0 | BytePositionInLine: 23.` with `Path == "$"` — it does not fall back to the base type. Hence the converter. (b) With the converter the discriminator survives: the bare answer's runtime type is exactly `TypeSafe.Answer`, its `Type` property is `""` (the base declares no primitive), and `AdditionalProperties` holds the whole object in wire order — `AdditionalProperties["type"] == "rank"` plus `order`. Because `type` is then ordinary extension data, serializing the bare answer back through the context reproduces `{"type":"rank","order":["a","b"]}` byte for byte (`FallBackToBaseType` writes no discriminator of its own for the base type). Both are asserted in `JsonContextTests` (`AnUnknownDiscriminatorReadStraightThroughTheContextIsARejectedRead`, `AnAnswerTypeTheSdkDoesNotModelDecodesWholeIntoExtensionData`).
- **Decision**: An unmodelled answer is **kept** in `SystemOneResponse.Answers` as a bare `Answer`, not dropped. It is excluded from `Nouls`, `Choices` and `Scores` for free, because those views match on the derived records. `FromBody` still warns once per such answer through the transport logger, at `Warning` level, with the upstream wording preserved verbatim: `Ignoring answer "<name>" with unrecognized type "<type>"`, where `<type>` comes from `AdditionalProperties["type"]` (the empty string if it is absent or not a string). This is a deliberate divergence from the Python SDK, which drops the answer entirely.
- **Consequences**: A caller on an older SDK can inspect a newer server's answer — `response.Answers["mystery"].AdditionalProperties` — instead of only reading it back off `RawHttpResponse`; both routes stay open. `Answers.Count` now counts unmodelled answers, so code that compared it against `Nouls.Count + Choices.Count + Scores.Count` may need updating, and `Answers[name]` can hand back an `Answer` that is none of the three derived types — the typed views remain the safe way to read answers. The message still says "Ignoring", which now means "ignored by the typed views" rather than "discarded": the wording is kept for log parity with the Python and JavaScript SDKs. Recorded in the README "Differences" section. Supersedes the drop described in the TSDK-8 entry below.

## Questions are polymorphic records written by the context; positional parameters are PascalCase
- **Date**: 2026-09-18
- **Status**: Accepted (story TSDK-9)
- **Context**: `Question` and its three types were classes with hand-written `internal JsonObject ToJson(string name)` methods that built the wire object (and validated it) by hand, the last writer-side reflection-free-but-manual path left after TSDK-8 moved reading onto `TypeSafeJsonContext`.
- **Decision**: `Question` is an abstract `record` with `[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]` and `[JsonDerivedType]` for `NoulQuestion` `noul`, `ChoiceQuestion` `choice` and `ScoreQuestion` `score`; `NoulCriteria` is a record too. The three question types are positional records (`NoulQuestion(JsonNode? Instructions, NoulCriteria? Criteria)`, `ChoiceQuestion(JsonNode? Instructions, IReadOnlyDictionary<string, JsonNode?> Criteria)`, `ScoreQuestion(JsonNode? Instructions, IReadOnlyList<JsonNode?> Criteria)`), so their constructor parameters are now PascalCase (`instructions` → `Instructions`, `criteria` → `Criteria`) and they gain value equality, `with` and `Deconstruct`. `Question.Type` is `[JsonIgnore]` on the base *and* on every override, because the source generator does not inherit `[JsonIgnore]` from a base declaration; optional fields (`Instructions`, `NoulQuestion.Criteria`, `NoulCriteria.WhenTrue`/`WhenFalse`) carry `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`, and `[JsonPropertyOrder]` pins `instructions` (1) ahead of `criteria` (2) so the wire order does not depend on where in the hierarchy a property is declared. The single writer-side entry point is `internal static JsonObject Question.Serialize(string name, Question question)`: it validates under the question's name and then calls `JsonSerializer.SerializeToNode(question, TypeSafeJsonContext.Default.Question)`. `RawQuestion` stays outside the polymorphic hierarchy — it is a record deriving from `Question` but is not a `[JsonDerivedType]` — and `Serialize` forwards a deep clone of its own `JsonObject` after `RawQuestion.Validated(name)` runs the same checks as before.
- **Consequences**: Wire output is byte-identical (`QuestionTests`' expected JSON strings pass unchanged) and every validation message is unchanged. Callers who used named arguments on a question constructor (`new NoulQuestion(instructions: x)`) must rename them; the `Question.Noul` / `Choice` / `Score` / `FromJson` builders, which the README and the demo use, keep their lowercase `Content` parameters and are unaffected. The question types are registered in `TypeSafeJsonContext` in the default generation mode rather than `JsonSourceGenerationMode.Serialization`, because the serialization-only fast path does not support `[JsonDerivedType]` (SYSLIB1039); the generated deserialization metadata is unused. The `ToJson` methods and the `Envelope()` helper are deleted; the request envelope is still assembled by hand in `TypeSafeClient.SystemOneAsync` until TSDK-9-6 moves it onto the context.

## `FieldPath` is the reader's JSON path, and unmodelled answer types are read by a converter
- **Date**: 2026-09-18
- **Status**: Accepted (story TSDK-8)
- **Context**: Transport now deserializes success bodies through the source-generated `TypeSafeJsonContext` instead of the hand-written `Wire` parser, so field paths come from `JsonException.Path` rather than being composed by hand. `System.Text.Json` blames the object it could not construct when a required member is missing, and writes list positions in brackets. Separately, `JsonUnknownDerivedTypeHandling.FallBackToBaseType` only affects serialization: reading an answer whose `type` this SDK version does not model throws.
- **Decision**: `TypeSafeApiResponseValidationException.FieldPath` is `JsonException.Path` with the leading `$.` stripped (`$` alone, and an exception with no path, become `""`). So `answers.tone.confidence` for a wrongly typed value, `answers.tone` for an answer missing `confidence`, `models[0]` for a list position, and `""` for a defect in the whole body (missing top-level member, malformed JSON, empty body). `SystemOneBody.Answers` is read by `AnswerMapConverter`, which peeks each entry's discriminator: a known one is read through the context, an unknown one becomes a bare `Answer` with every field (including `type`) in `AdditionalProperties`, and a missing or non-string `type`, or an entry that is not an object, stays a read error at the same path the `Wire` parser used.
- **Consequences**: Validation messages for a missing member name the containing object rather than the member; the `ResponseTests` expectations were reconciled to the reader's paths. `SystemOneResponse.FromBody` warns about and drops bare answers, so `Answers`, `Nouls`, `Choices` and `Scores` only ever hold modelled answers, exactly as before; the raw payload stays reachable through `RawHttpResponse`. (Superseded in TSDK-10: a bare answer is now kept in `Answers` — see the entry at the top of this file.) The converter composes error paths itself (`$.answers.<name>…`) because a nested `JsonSerializer` call reports paths relative to its own root. With the reader in place the hand-written parser is gone: `Wire`, `ResponseFieldException`, `SystemOneResponse.Parse`, `ListModelsResponse.Parse` and `JsonContent.AsDouble` are deleted (~175 lines). All five were `internal`, so the public surface is unchanged — `SystemOneResponse` and `ListModelsResponse` keep every public member and are now built only through `FromBody`.

## `TypeSafeClientOptions` is init-only
- **Date**: 2026-09-18
- **Status**: Accepted (story TSDK-3)
- **Context**: The options object had public setters, so it read as live configuration even though `TypeSafeClient` copies every value in its constructor and never looks at the instance again. Mutating options after construction silently did nothing.
- **Decision**: Every `TypeSafeClientOptions` property is `{ get; init; }`, matching `RequestOptions` and `RetryPolicy`. Callers configure the client in an object initializer; there is no post-construction mutation to mislead them.
- **Consequences**: Code that built an options instance and then assigned to it must move the assignments into the initializer. The test helper `Clients.Create` keeps its `o => o.X = ...` ergonomics through a mutable test-only `ClientSetup` holder that is applied in the initializer.

## `ListModelsResponse` is the list; answers gain `Probability` / `Label` aliases
- **Date**: 2026-09-18
- **Status**: Accepted (story TSDK-3)
- **Context**: `client.Models.ListAsync()` returned a response whose only content was a `Models` property, so every caller wrote `models.Models`. Separately, the earlier "Answer property names mirror the wire format" decision deferred the `Probability` / `Label` aliases.
- **Decision**: `ListModelsResponse` implements `IReadOnlyList<ModelMetadata>` (`Count`, indexer, `GetEnumerator`) over a private backing list, and the `Models` property is removed — one way to reach the models, not two. `NoulAnswer.Probability` and `ChoiceAnswer.Label` are added as get-only computed aliases of `Noul` and `Choice`, marked `[JsonIgnore]` so a future source-generated `JsonSerializerContext` (planned in TSDK-7) cannot leak them onto the wire.
- **Consequences**: C#-side surface changes only; wire parity unchanged. Callers using `response.Models` iterate or index the response itself. The wire names stay primary in the docs, with the aliases documented as conveniences.

## HTTP status codes are exposed as `HttpStatusCode`
- **Date**: 2026-09-18
- **Status**: Accepted (story TSDK-3)
- **Context**: The port carried the upstream integer `status` through `ApiResponse.Status`, `TypeSafeApiException.Status` and the exception constructors, with `StatusCode` bolted on as a derived enum property.
- **Decision**: `ApiResponse` and `TypeSafeApiException` expose `HttpStatusCode StatusCode` only; the exception constructors and `TypeSafeApiException.FromResponse` take an `HttpStatusCode`. No `int Status` and no obsolete shims — this is the breaking-change release. `RetryPolicy.HttpStatuses` stays `IReadOnlySet<int>` so callers can list codes the enum does not name.
- **Consequences**: A C#-side naming and type change only; exception messages still start with the numeric code, so wire and log parity are unchanged. Callers reading `Status` must switch to `StatusCode` and cast when they want the number.

## `RetryPolicy` retry flags are named as actions
- **Date**: 2026-09-18
- **Status**: Accepted (story TSDK-3)
- **Context**: The port carried the upstream error-type names `ApiConnectionError`, `ApiTimeoutError` and `Predicate`, which read as error objects rather than as the switches they are.
- **Decision**: Rename to `RetryConnectionErrors`, `RetryTimeouts` and `RetryWhen`, per the naming table from the typing review. No obsolete shims — this is the breaking-change release.
- **Consequences**: A C#-side naming change only; the wire format and retry behaviour are unchanged. Callers setting the old names must rename.

## Retarget to net10.0 and adopt C# 14 features
- **Date**: 2026-09-18
- **Status**: Accepted (epic TSDK-1)
- **Context**: .NET 9 (STS) left support in May 2026 and .NET 8 (LTS) leaves in November 2026. The port targets net8.0 and cannot use C# 13/14 or the .NET 9 System.Text.Json features (`AllowOutOfOrderMetadataProperties`, `RespectRequiredConstructorParameters`, `JsonSerializerOptions.Web`).
- **Decision**: Single `net10.0` target; enable `IsAotCompatible`; apply the naming table from the typing review.
- **Consequences**: Drops the `RollForward=Major` workaround. Consumers on .NET 8 cannot use versions after this change.
- **Update (2026-09-18, task TSDK-2-4)**: `<IsAotCompatible>true</IsAotCompatible>` is now set permanently in
  `src/TypeSafe.Sdk/TypeSafe.Sdk.csproj`. It implies `IsTrimmable`, `EnableTrimAnalyzer`, `EnableAotAnalyzer` and
  `EnableSingleFileAnalyzer` (verified with `dotnet msbuild -getProperty:`), so the trim/single-file/AOT analyzers run
  on every build. A full `dotnet build --no-incremental` of the solution reports **0 warnings**: every
  `JsonSerializer` call now goes through `Internal/TypeSafeJsonContext`, and the only members carrying
  `RequiresUnreferencedCode` / `RequiresDynamicCode` are `Content.From(object)`, `Internal.JsonContent.From(object)`
  and the `Internal.JsonContent.SerializerOptions` getter. `PublicSurfaceTests.OnlyTheReflectionConversionRequiresUnreferencedOrDynamicCode`
  asserts that set by reflection, so a new unannotated reflection path fails the suite.
- **Publish check on this machine (2026-09-18)**: `dotnet publish examples/TypeSafe.Sdk.Demo -c Release -r linux-x64
  -p:PublishAot=true` **succeeded end to end** on the first attempt — ILCompiler restored, "Generating native code"
  emitted no IL warnings, and the native binary landed at
  `examples/TypeSafe.Sdk.Demo/bin/Release/net10.0/linux-x64/publish/TypeSafe.Sdk.Demo` (7.1 MB, stripped ELF). No
  `-p:CppCompilerAndLinker=gcc` override was needed (gcc at `/usr/bin/gcc`, no clang installed). The
  toolchain-independent `-p:PublishTrimmed=true --self-contained` publish also ran ILLink to completion with 0
  warnings. The demo uses `Content.From(state, DemoJsonContext.Default.DemoState)`, so it never touches the
  annotated reflection overload; a consumer that does will see `IL2026`/`IL3050` at their own call site by design.

## Replace `object?` inputs with a `Content` value type
- **Date**: 2026-09-18
- **Status**: Accepted (epic TSDK-2)
- **Context**: Builders and `state` accept `object?` and run reflection serialisation with camelCase naming. That is the only untyped, trim-unsafe part of the surface and it renames consumer properties silently.
- **Decision**: `readonly record struct Content(JsonNode? Node)` with implicit conversions from `string`, `JsonObject`, `JsonArray`; `Content.From<T>(T, JsonTypeInfo<T>)` for source-generated callers; the reflection overload kept but annotated `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]`.
- **Consequences**: Breaking change to builder signatures. Anonymous-object callers must use `Content.From(obj)` explicitly.

## Async-only public API
- **Date**: 2026-09-18
- **Status**: Accepted
- **Context**: Python ships sync and async clients. Sync-over-async in .NET is a deadlock hazard.
- **Decision**: `SystemOneAsync` and `ListAsync` only, with `CancellationToken`.
- **Consequences**: Documented in README "Differences" section.

## Answer property names mirror the wire format
- **Date**: 2026-09-18
- **Status**: Accepted
- **Context**: `NoulAnswer.Noul` and `ChoiceAnswer.Choice` read oddly in C#, but match the docs and the other SDKs.
- **Decision**: Keep wire names; add `Probability` / `Label` aliases later rather than rename.
- **Consequences**: Cross-SDK docs stay applicable. The aliases landed in story TSDK-3 — see the entry at the top of this file.

## Caller cancellation is `OperationCanceledException`
- **Date**: 2026-09-18
- **Status**: Accepted
- **Context**: JS has `APIUserAbortError`; .NET convention is the framework exception.
- **Decision**: Rethrow as `OperationCanceledException` carrying the caller's token; timeouts are `TypeSafeApiTimeoutException`.

## Retry budget (`TotalTimeout`) present but off by default
- **Date**: 2026-09-18
- **Status**: Accepted
- **Context**: Python defaults to a 30 s retry budget; JS has none.
- **Decision**: Expose `RetryPolicy.TotalTimeout`, default `null`, matching JS.

## Score criteria minimum is one
- **Date**: 2026-09-18
- **Status**: Accepted
- **Context**: Python requires ≥1 criterion, JS requires ≥2 at the same version.
- **Decision**: Follow the permissive Python rule; the server is the arbiter.

## Supplied `HttpClient` is not disposed unless `DisposeHttpClient` is set
- **Date**: 2026-09-18
- **Status**: Accepted
- **Context**: Python closes a supplied client; .NET convention is not to dispose what you don't own (`IHttpClientFactory` clients especially).
- **Decision**: Opt-in disposal.

## Hand-written JSON parsing (JsonNode) for the initial port
- **Date**: 2026-09-18
- **Status**: Superseded (story TSDK-8) by the source-generated `TypeSafeJsonContext`; `Wire` is deleted
- **Context**: On net8.0, System.Text.Json polymorphism needs the discriminator first and cannot fall back for unknown types cleanly; the Python SDK's forward-compat rules required a manual parser.
- **Decision**: `Wire` helper with explicit field paths.
- **Consequences**: ~200 lines to delete once on .NET 10.

---
*Last reviewed: 2026-09-18*
