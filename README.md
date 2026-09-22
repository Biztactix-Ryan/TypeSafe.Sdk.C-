# TypeSafe AI C# SDK

[![CI](https://github.com/Biztactix-Ryan/TypeSafe.Sdk.C-/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Biztactix-Ryan/TypeSafe.Sdk.C-/actions/workflows/ci.yml)

C# SDK for [TypeSafe AI](https://typesafe.ai), ported from the official
[Python](https://github.com/typesafe-ai/typesafe-sdk-python) and
[JavaScript](https://github.com/typesafe-ai/typesafe-sdk-js) SDKs (v0.6.0).

## Requirements

- Targets `net10.0`, so consuming projects need the .NET 10 runtime (and the .NET 10 SDK to build
  from source). Earlier targets are not multi-targeted.
- The only dependency is `Microsoft.Extensions.Logging.Abstractions`. The optional
  `TypeSafe.Sdk.DependencyInjection` package adds `Microsoft.Extensions.Http` and
  `Microsoft.Extensions.Options.ConfigurationExtensions` on top, for applications that want them.
- The library is built with `IsAotCompatible`, so it is marked trimmable and ships clean under
  `PublishTrimmed` and `PublishAot`. All JSON goes through a source-generated serializer context; the
  one reflection path is the `Content.From(object?)` overload described under
  [Content](#content), which is annotated so your call site — not the runtime — tells you about it.

If you are still on .NET 8, stay on version 0.6.0 — it is the last release that targets `net8.0`.

## Installation

```sh
dotnet add package TypeSafe.Sdk
```

Or add the reference by hand:

```xml
<PackageReference Include="TypeSafe.Sdk" Version="0.7.0" />
```

Registering the client in a dependency-injection container — `services.AddTypeSafeClient(...)` with
`IHttpClientFactory` and the options pattern — is a second, optional package, so the SDK itself
keeps its one dependency. See [Dependency injection](#dependency-injection).

```sh
dotnet add package TypeSafe.Sdk.DependencyInjection
```

| Package                             | Contents                                                     | Depends on                                                                            |
| ----------------------------------- | ------------------------------------------------------------ | -------------------------------------------------------------------------------------- |
| `TypeSafe.Sdk`                      | The client, questions, responses, retries                     | `Microsoft.Extensions.Logging.Abstractions`                                            |
| `TypeSafe.Sdk.DependencyInjection`  | `AddTypeSafeClient` for `IServiceCollection`                  | `TypeSafe.Sdk`, `Microsoft.Extensions.Http`, `Microsoft.Extensions.Options.ConfigurationExtensions` |

Both package ids live on [nuget.org](https://www.nuget.org/packages/TypeSafe.Sdk) — there is no
other feed — and both are released together, at the same version, from one tag. Releases are cut
from version tags: pushing a `v*` tag (`v0.7.0` for the version above) runs
[`.github/workflows/release.yml`](.github/workflows/release.yml), which checks the tag against the
version in the sources, packs both projects, and pushes them to nuget.org. Each release also carries
a symbols package (`.snupkg`) and SourceLink, so a debugger can step into the SDK's own sources.

.NET 8 consumers stay on 0.6.0, the last `net8.0` release — see [Requirements](#requirements) above.

## About this repository

I wrote this repository while waiting to be given access to TypeSafe AI. It is an unofficial,
community port: the public API, retry behaviour, error mapping, and header handling are translated
directly from the official Python and JavaScript SDKs at version 0.6.0, so it should behave the
same way. Until I have API access, the test suite runs against a stubbed HTTP handler rather than
the live service, so treat the first real requests as a shakedown and please open an issue if the
wire format has drifted.

Ryan, [Biztactix](https://biztactix.com.au)

## Quickstart

Install the package as above (or reference the `src/TypeSafe.Sdk` project), set
`TYPESAFE_API_KEY` in your environment, then create and use the client:

```csharp
using TypeSafe;

using var client = new TypeSafeClient();

var billing = Question.Named.Noul("billing", "Is this ticket about billing?");
var tone = Question.Named.Choice("tone", "What is the customer's tone?", "calm", "frustrated", "angry");

var response = await client.SystemOneAsync(
    state: Content.From(new { document = "I was charged twice. Please fix this ASAP." }),
    questions: [billing, tone]);

Console.WriteLine(response.Get(tone).Choice);         // "frustrated"
Console.WriteLine(response.Get(billing).Probability); // 0.93
```

Each question carries the name its answer comes back under, so `response.Get(question)` needs no
string and hands back the answer type that question is answered with: `Get(tone)` is a
`ChoiceAnswer` and `Get(billing)` a `NoulAnswer`, both checked at compile time. Answers are also
available by name, grouped by question type: `response.Nouls`, `response.Choices`, and
`response.Scores`, or all together in `response.Answers`.

## Content

The request state and every question instruction and description is a `Content` value. Three inputs
convert implicitly — a `string`, a `JsonObject`, and a `JsonArray` — and `Content.Null` (the default
value) leaves the content unset:

```csharp
await client.SystemOneAsync("I was charged twice.", questions);
await client.SystemOneAsync(new JsonObject { ["document"] = "I was charged twice." }, questions);
await client.SystemOneAsync(new JsonArray { "I was charged twice.", "Any update?" }, questions);
await client.SystemOneAsync(Content.Null, questions);
```

Anything else is converted explicitly. `Content.From(object?)` takes any .NET value — anonymous
types, records, dictionaries — and serializes it by reflection with `System.Text.Json` web defaults,
so property names are camelCased (`PriorityLevel` becomes `priorityLevel`):

```csharp
var state = Content.From(new { subject = "Charged twice", priorityLevel = 2 });
```

That overload reflects over the type, so it is annotated with `RequiresUnreferencedCode` and
`RequiresDynamicCode`: in a trimmed or native AOT app the call site reports `IL2026` and `IL3050` at
build time instead of failing at run time. There, and anywhere the shape of the state is known, use
`Content.From<T>(T, JsonTypeInfo<T>)` with a source-generated `JsonSerializerContext`. Property
naming then follows that context's options rather than the SDK's camelCase default:

```csharp
using System.Text.Json.Serialization;
using TypeSafe;

public record Ticket(string Subject, string Body);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Ticket))]
public partial class AppJsonContext : JsonSerializerContext;

var ticket = new Ticket("Charged twice this month", "Two charges of $49 on one account.");

var response = await client.SystemOneAsync(
    state: Content.From(ticket, AppJsonContext.Default.Ticket),
    questions: questions);
```

## Questions

There are two families of builders. The name-first ones on `Question.Named` take the name the answer
comes back under and return a `Named<TAnswer>`: the question object is then the key, and the
`TAnswer` in its type is what [`response.Get`](#responses) returns. Instructions and descriptions are
[`Content`](#content), so text, a `JsonObject`, and a `JsonArray` convert implicitly and
`Content.Null` leaves one unset.

```csharp
// Yes/no, with optional descriptions of either outcome.
Named<NoulAnswer> billing = Question.Named.Noul("billing", "Is this ticket about billing?", whenTrue: "mentions charges or invoices");

// Select between labels, undescribed or described.
Named<ChoiceAnswer> tone = Question.Named.Choice("tone", "What is the customer's tone?", "calm", "frustrated", "angry");
Named<ChoiceAnswer> tier = Question.Named.Choice("tier", "Which plan?", new Dictionary<string, Content>
{
    ["free"] = "no payment on file",
    ["pro"] = Content.Null,
});

// Ordered rubric, one description per score from zero.
Named<ScoreAnswer> urgency = Question.Named.Score("urgency", "How urgent is this?", "can wait", "this week", "today", "right now");

var response = await client.SystemOneAsync(state, [billing, tone, tier, urgency]);
```

A choice or a score question can also take its labels or its rubric from an enum, so the answer comes
back as the enum itself rather than as a string or an integer — see [Enums](#enums).

### Naming questions in a dictionary

The positional builders on `Question` keep the shape the Python and JavaScript SDKs use: the names
live in a dictionary and the answers are read back out of the grouped views by the same strings.
Use this form when porting code from those SDKs, when the questions are assembled from data rather
than written out, and for `Question.FromJson`, which has no name-first form because the SDK cannot
know what a raw question is answered with.

```csharp
var questions = new Dictionary<string, Question>
{
    ["isBilling"] = Question.Noul("Is this ticket about billing?", whenTrue: "mentions charges or invoices"),
    ["tone"] = Question.Choice("What is the customer's tone?", "calm", "frustrated", "angry"),
    ["urgency"] = Question.Score("How urgent is this?", "can wait", "this week", "today", "right now"),

    // Raw JSON for question types or fields this SDK version does not model.
    ["future"] = Question.FromJson(new JsonObject { ["type"] = "noul", ["instructions"] = "Really?", ["extra"] = 1 }),
};

var response = await client.SystemOneAsync(state, questions);
Console.WriteLine(response.Choices["tone"].Choice);
```

## Enums

`Question.Named.Choice<TEnum>` takes a choice question's labels from an enum and
`Question.Named.Score<TEnum>` takes a score question's rubric from one. The answer is then keyed by
that enum — `ChoiceAnswer<TEnum>` and `ScoreAnswer<TEnum>` — so no label and no rubric level is ever
spelled twice, and a typo is a compile error rather than a missing dictionary key.

```csharp
using System.ComponentModel;
using System.Text.Json.Serialization;
using TypeSafe;

public enum Tone
{
    Calm,
    Frustrated,
    [JsonStringEnumMemberName("very_angry")]
    VeryAngry,
}

public enum Urgency
{
    [Description("can wait")]
    Low = 0,
    [Description("today")]
    Medium = 1,
    [Description("right now")]
    High = 2,
}

Named<ChoiceAnswer<Tone>> tone = Question.Named.Choice<Tone>("tone", "What is the customer's tone?");
Named<ScoreAnswer<Urgency>> urgency = Question.Named.Score<Urgency>("urgency", "How urgent is this?");

var response = await client.SystemOneAsync(state, [tone, urgency]);

ChoiceAnswer<Tone> toneAnswer = response.Get(tone);
toneAnswer.Choice;                          // Tone.Frustrated
toneAnswer.Confidence;
toneAnswer.Probabilities[Tone.VeryAngry];   // 0.12

ScoreAnswer<Urgency> urgencyAnswer = response.Get(urgency);
urgencyAnswer.Score;                        // 1.6
urgencyAnswer.Nearest;                      // Urgency.High
urgencyAnswer.Legend[Urgency.High];         // "right now" (JsonNode)
urgencyAnswer.Probabilities[Urgency.High];  // 0.6
```

**Labels.** A member's wire label is its `[JsonStringEnumMemberName("...")]` when it carries one and
the snake_case of its name otherwise, which is what a source-generated context would write: `Calm`
is `calm` and `VeryAngry` is `very_angry`. Two members that end up with the same label are a
`TypeSafeException`.

**Descriptions.** `[System.ComponentModel.Description]` is what the model is told about a member: it
becomes the criteria text of a choice label (an undescribed label is sent as `null`) and the rubric
text of a score level (an undescribed level falls back to its wire label). Nothing else about the
request changes — the body is byte-for-byte the one `Question.Named.Choice(...)` and
`Question.Named.Score(...)` produce from the same labels and criteria, so the enum exists only on
this side of the wire.

**Score enums are rubrics.** A rubric is a list indexed by score from zero, so a score enum's members
must be numbered exactly `0` to `N-1` in declaration order. One that is not — numbered from one,
skipping a value, or declaring no members at all — throws `TypeSafeException` from the builder:
`Enum Urgency must have contiguous values starting at 0 to be used as a score rubric.` A choice enum
has no such rule; only its labels matter. `Nearest` is the score rounded half away from zero (`1.4`
is `Medium`, `1.5` is `High`) and clamped to the rubric.

**Undeclared labels are invalid response data.** Every label and every score the server sends must be
one the enum declares. One that is not is reported rather than quietly dropped: `response.Get` throws
`TypeSafeApiResponseValidationException` with `FieldPath` `answers.tone.choice` — or
`answers.tone.probabilities.<label>`, `answers.urgency.legend.<score>`,
`answers.urgency.probabilities.<score>` for the key that was not recognised — and `TryGet` returns
`false` instead. Widen the enum (or read the answer through the untyped `response.Choices` /
`response.Scores`) when the server may answer with labels this build does not know.

## Responses

`response.Get(question)` takes one of the `Named<TAnswer>` values the [name-first
builders](#questions) return and gives back that `TAnswer`. It throws a `TypeSafeException` when
the response carries no answer under the question's name, or one of another type. `TryGet` is the
non-throwing form.

```csharp
var billingAnswer = response.Get(billing);
billingAnswer.Noul;             // 0.93
billingAnswer.Probability;      // the same value, under a .NET-style name

var toneAnswer = response.Get(tone);
toneAnswer.Choice;              // "frustrated"
toneAnswer.Label;               // the same value, under a .NET-style name
toneAnswer.Confidence;
toneAnswer.Probabilities["angry"];

if (response.TryGet(urgency, out var urgencyAnswer))
{
    urgencyAnswer.Score;            // expected score, e.g. 2.4
    urgencyAnswer.Confidence;
    urgencyAnswer.Legend[2];        // "today" (JsonNode)
    urgencyAnswer.Probabilities[3]; // 0.5
}
```

Every answer is also available by name, grouped by question type, which is how the
[dictionary form](#naming-questions-in-a-dictionary) reads them:

```csharp
response.Nouls["isBilling"].Probability;
response.Choices["tone"].Choice;
response.Scores["urgency"].Score;
response.Answers["tone"];   // the same answer, as the base Answer

response.GetAt(0);        // the first answer the server wrote: KeyValuePair<string, Answer>
response.Count;           // how many answers came back
response.Model;
response.Usage.InputTokens;
response.RequestId;       // x-typesafe-request-id, or null
response.RawHttpResponse; // the buffered HttpResponseMessage
```

`response.Answers` preserves wire order: it is an `OrderedDictionary<string, Answer>` behind its
`IReadOnlyDictionary` face, so it enumerates in the order the server wrote the answers rather than in
hash order, `response.GetAt(index)` reads it by position, and the grouped views keep that order too.
`Nouls`, `Choices` and `Scores` are also extension members on any `IReadOnlyDictionary<string, Answer>`,
so a map you assembled yourself has the same three views.

Answer types this SDK does not recognise are skipped with a warning; the raw payload remains
available through `RawHttpResponse`.

## Models

`ListModelsResponse` is an `IReadOnlyList<ModelMetadata>`, so enumerate or index the response itself.

```csharp
var models = await client.Models.ListAsync();
foreach (var model in models)
    Console.WriteLine($"{model.Name}: {model.Description} ({model.ReleaseDate})");

Console.WriteLine($"{models.Count} models; first is {models[0].Name}");
```

## Configuration

Explicit options take precedence over environment variables, then SDK defaults. Empty or
whitespace-only environment values are ignored. `TypeSafeClientOptions` is a `sealed record` whose
properties are `init`-only, so set them in the object initializer; the client reads them once, while
it is being constructed. A variant is derived with `options with { Timeout = ... }`, and two option
sets holding the same values compare equal. Printing one is safe: `ToString()` shows the `ApiKey`
only as `[redacted]` and omits `DefaultHeaders` altogether.

| Option           | Environment variable     | Default                   |
| ---------------- | ------------------------ | ------------------------- |
| `ApiKey`         | `TYPESAFE_API_KEY`       | required                  |
| `BaseUrl`        | `TYPESAFE_BASE_URL`      | `https://api.typesafe.ai` |
| `DefaultModel`   | `TYPESAFE_DEFAULT_MODEL` | `jev-latest`              |
| `LogLevel`       | `TYPESAFE_LOG_LEVEL`     | `warn`                    |
| `Timeout`        |                          | 10 seconds per attempt    |
| `Retry`          |                          | `RetryPolicy.Default`     |
| `DefaultHeaders` |                          | none                      |
| `TimeProvider`   |                          | `TimeProvider.System`     |

```csharp
using var client = new TypeSafeClient(new TypeSafeClientOptions
{
    ApiKey = "...",
    Timeout = TimeSpan.FromSeconds(30),
    Retry = RetryPolicy.Default with { MaxRetries = 3, TotalTimeout = TimeSpan.FromSeconds(60) },
    DefaultHeaders = new Dictionary<string, string> { ["X-Team"] = "support" },
    HttpClient = httpClientFromFactory,   // optional; or HttpMessageHandler for tests
    Logger = loggerFactory.CreateLogger("TypeSafe"),
    LogLevel = LogLevel.Information,
});
```

Per-call overrides go in `RequestOptions`, a `sealed record` with the same `init`-only style:

```csharp
await client.SystemOneAsync(state, questions, model: "jev-latest", options: new RequestOptions
{
    Timeout = TimeSpan.FromSeconds(5),
    Retry = client.Retry with { MaxRetries = 0 },
    Headers = new Dictionary<string, string> { ["X-Trace"] = traceId },
}, cancellationToken);
```

Authentication, SDK identification, `Accept`, and `Content-Type` headers are protected and cannot
be overridden.

### Retries

`RetryPolicy` mirrors the other SDKs: two retries by default with exponential backoff from 500ms
to 5s and 25% jitter, honouring `Retry-After` and `retry-after-ms` up to 60s. Retried failures are
HTTP 408, 429, and 5xx responses, connection errors, and timeouts. `RetryWhen` adds custom rules,
`TotalTimeout` caps the whole call, and `RetryPolicy.None` disables retries. Retry sleeps,
`Retry-After` date maths and elapsed-time logging all read the `TimeProvider` option, so a test can
pass a fake clock instead of waiting on real delays.

### Logging

The SDK logs through `Microsoft.Extensions.Logging.ILogger`, defaulting to a `[typesafe-sdk]`
prefixed logger on standard error. `info` logs request summaries; `debug` adds headers and bodies.
Credential headers are redacted; bodies are not.

Every event is written through a `[LoggerMessage]` source-generated method, so a structured sink
sees a stable `EventId` and named properties rather than message text to parse. Event ids are part
of the SDK's observable contract and are never reused or renumbered.

| Id     | Name                            | Level  | Properties                                                            |
| ------ | ------------------------------- | ------ | --------------------------------------------------------------------- |
| `1001` | `RequestSending`                | Debug  | `RequestNumber`, `Method`, `Path`, `Url`, `Attempt`, `Headers`, `Body` |
| `1002` | `ResponseReceived`              | Info   | `RequestNumber`, `Method`, `Path`, `StatusCode`, `ElapsedMs`, `Attempt` |
| `1003` | `ResponseReceivedWithRequestId` | Info   | the above plus `RequestId`                                            |
| `1004` | `ResponseBody`                  | Debug  | `RequestNumber`, `Method`, `Path`, `Headers`, `Body`                   |
| `1005` | `RequestCancelled`              | Info   | `RequestNumber`, `Method`, `Path`, `ElapsedMs`                         |
| `1006` | `AttemptTimedOut`               | Info   | `RequestNumber`, `Method`, `Path`, `ElapsedMs`, `TimeoutMs`            |
| `1007` | `ConnectionError`               | Info   | `RequestNumber`, `Method`, `Path`, `ElapsedMs`, `Error`                |
| `1008` | `RetryBudgetExceeded`           | Info   | `RequestNumber`, `Method`, `Path`, `DelayMs`, `BudgetMs`               |
| `1009` | `RetryScheduled`                | Info   | `RequestNumber`, `Method`, `Path`, `DelayMs`, `Retry`, `TotalRetries`, `Reason` |
| `1010` | `RetryCancelled`                | Info   | `RequestNumber`, `Method`, `Path`, `Attempt`                           |
| `1011` | `UnknownAnswerType`             | Warn   | `Question`, `Type`                                                     |

`RequestNumber` counts calls made through one client, `Attempt` is 1-based within a call, `Path` is
the request path and `Url` the absolute target. A response carrying no request id is reported as
`ResponseReceived`; one that names it as `ResponseReceivedWithRequestId`.

## Dependency injection

`TypeSafe.Sdk.DependencyInjection` is a separate package so the core SDK keeps its one dependency:
only an application that wants container registration pays for `Microsoft.Extensions.Http` and the
options binder.

```sh
dotnet add package TypeSafe.Sdk.DependencyInjection
```

`AddTypeSafeClient` registers `TypeSafeClient` and `ITypeSafeClient`, binds their options from the
`TypeSafe` configuration section, builds the client on an `IHttpClientFactory` client named
`TypeSafe`, and returns that client's `IHttpClientBuilder`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using TypeSafe;

// services is builder.Services and configuration is builder.Configuration
// in an ASP.NET Core or worker-service application.
services.AddTypeSafeClient(configuration);
```

Passing the configuration root binds its `TypeSafe` section; passing a section
(`configuration.GetSection("Integrations:TypeSafe")`) binds that section instead. The overload
without a configuration argument registers the client with no binding at all, leaving it on the
`TYPESAFE_*` environment variables and the code overrides below.

Anything the client needs is then injected:

```csharp
public sealed class TriageService(ITypeSafeClient typeSafe)
{
    public Task<SystemOneResponse> TriageAsync(string ticket, CancellationToken cancellationToken) =>
        typeSafe.SystemOneAsync(
            ticket,
            new Dictionary<string, Question> { ["billing"] = Question.Noul("Is this a billing issue?") },
            cancellationToken: cancellationToken);
}
```

Both registrations are **transient**: each resolution is a new client over a factory-managed
`HttpClient`, which is the lifetime `IHttpClientFactory` is built for — the factory pools the
handler, so a new client per request is not a new connection pool. Resolved clients do not dispose
the factory's `HttpClient`, so injecting `ITypeSafeClient` into a scoped or transient service and
forgetting about it is safe. If an `ILoggerFactory` or a `TimeProvider` is in the container, the
client uses it.

### The `TypeSafe` configuration section

```json
{
  "TypeSafe": {
    "BaseUrl": "https://api.typesafe.ai",
    "DefaultModel": "jev-latest",
    "Timeout": "00:00:30",
    "LogLevel": "Information",
    "DefaultHeaders": {
      "X-Team": "support"
    },
    "Retry": {
      "MaxRetries": 3,
      "BackoffInitial": "00:00:00.500",
      "BackoffMax": "00:00:05",
      "BackoffJitter": 0.25,
      "HttpStatuses": [408, 429, 500, 502, 503, 504],
      "RespectRetryAfter": true,
      "MaxRetryAfter": "00:01:00",
      "RetryConnectionErrors": true,
      "RetryTimeouts": true,
      "TotalTimeout": "00:01:00"
    }
  }
}
```

Every key is optional. `Timeout` and the `Retry` durations are `TimeSpan` strings (`"00:00:30"`);
`Retry` keys that are left out keep their `RetryPolicy.Default` value, and `HttpStatuses` replaces
the retried set rather than adding to it. `LogLevel` is spelled with the
`Microsoft.Extensions.Logging` names — `Trace`, `Debug`, `Information`, `Warning`, `Error`, `None` —
not the `TYPESAFE_LOG_LEVEL` spellings. A key that is absent falls back to the matching
`TYPESAFE_*` environment variable and then to the SDK default, so `ApiKey` is best left out of
`appsettings.json` and supplied through `TYPESAFE_API_KEY`, user secrets, or a secret store.

### Overriding in code

`TypeSafeClientOptions` is an `init`-only record, so the `configure` callback is a
`Func<TypeSafeClientOptions, TypeSafeClientOptions>`: it derives a new value with `with` and returns
it rather than mutating one. It runs after binding, so code wins over configuration and the
environment:

```csharp
services.AddTypeSafeClient(configuration, options => options with
{
    DefaultModel = "jev-latest",
    DefaultHeaders = new Dictionary<string, string> { ["X-Tenant"] = tenant },
    Retry = (options.Retry ?? RetryPolicy.Default) with { MaxRetries = 1 },
});
```

`HttpClient` and `HttpMessageHandler` are rejected here — the transport belongs to
`IHttpClientFactory` — and setting either throws when the client is resolved. Configure the
transport on the returned builder instead.

### Handlers on the returned `IHttpClientBuilder`

The return value is the `IHttpClientBuilder` for the named client, so the usual extensions apply —
a resilience handler, an outbound logging handler, a primary handler with a proxy or a certificate:

```csharp
services.AddTypeSafeClient(configuration)
        .AddStandardResilienceHandler(resilience =>
            resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10));
```

`AddStandardResilienceHandler` comes from `Microsoft.Extensions.Http.Resilience`, which the DI
package does not reference — add it only if you want it. Its retries stack on top of the SDK's own,
so pick one: either leave the handler's retry pipeline in place and set `Retry = RetryPolicy.None`
in the `configure` callback, or keep the SDK's `RetryPolicy` (which alone honours `Retry-After` and
`retry-after-ms`) and use the handler for circuit breaking and concurrency limits. The named
client's `HttpClient.Timeout` is deliberately `InfiniteTimeSpan` — the SDK applies its own
per-attempt `Timeout` — so a handler timeout is the one to tune, not `HttpClient.Timeout`.

## Errors

| Exception                                | When                                                     |
| ---------------------------------------- | -------------------------------------------------------- |
| `TypeSafeException`                      | Base type; also invalid configuration or questions       |
| `TypeSafeApiException`                   | Any non-2xx response: `StatusCode`, `Body`, `Headers`, `RequestId` |
| `TypeSafeBadRequestException`            | 400                                                      |
| `TypeSafeAuthenticationException`        | 401                                                      |
| `TypeSafePermissionDeniedException`      | 403                                                      |
| `TypeSafeNotFoundException`              | 404                                                      |
| `TypeSafeUnprocessableEntityException`   | 422                                                      |
| `TypeSafeRateLimitException`             | 429, with `RetryAfter`                                   |
| `TypeSafeInternalServerException`        | 5xx                                                      |
| `TypeSafeApiResponseValidationException` | 2xx body missing required data, with `FieldPath`         |
| `TypeSafeApiConnectionException`         | No HTTP response: DNS, TLS, reset, interrupted body; `Error` carries the `HttpRequestError` when one was reported |
| `TypeSafeApiTimeoutException`            | Per-attempt timeout exceeded (a connection exception)    |
| `OperationCanceledException`             | The caller's `CancellationToken` was cancelled           |

```csharp
try
{
    var response = await client.SystemOneAsync(state, questions);
}
catch (TypeSafeRateLimitException error) when (error.RetryAfter is { } wait)
{
    await Task.Delay(wait);
}
catch (TypeSafeApiException error)
{
    Console.Error.WriteLine($"{(int)error.StatusCode} {error.Detail} (request {error.RequestId})");
}
```

## Testing your code

`ITypeSafeClient` and `IModelsResource` are provided for mocking, and every response and answer type
has a public constructor. To exercise the real client without the network, pass an
`HttpMessageHandler` through `TypeSafeClientOptions.HttpMessageHandler`.

## Development

Requires the .NET 10 SDK.

```sh
dotnet build
dotnet test
TYPESAFE_API_KEY=... dotnet run --project examples/TypeSafe.Sdk.Demo
```

The test suite snapshots the public API, so any change to a public signature fails a test until the
snapshot is re-approved — [CONTRIBUTING.md](CONTRIBUTING.md) explains how to review and accept it,
and what a breaking change has to carry with it.

### Continuous integration

Every push to `main` and every pull request runs
[`.github/workflows/ci.yml`](.github/workflows/ci.yml) on `ubuntu-latest` with the .NET 10 SDK:

- `dotnet restore`
- `dotnet build --no-restore -warnaserror` — the tree builds with zero warnings, so a new warning
  fails the build
- `dotnet test --no-build`
- `scripts/check-version.sh` — the version check
- `dotnet pack` of both packable projects — `src/TypeSafe.Sdk/TypeSafe.Sdk.csproj` and
  `src/TypeSafe.Sdk.DependencyInjection/TypeSafe.Sdk.DependencyInjection.csproj` — into `artifacts`,
  uploading the resulting `.nupkg` files as a build artifact

### Live tests

`tests/TypeSafe.Sdk.IntegrationTests` holds three tests that talk to the real API: a model listing, a
System One call with a noul, a choice and a score question, and a bad key that must come back as a
401. Every one of them is gated on `TYPESAFE_API_KEY` and reports itself as skipped — "TYPESAFE_API_KEY
is not set; live tests skipped" — when the variable is absent, so a plain `dotnet test` runs the whole
suite with the three live tests skipped. To run them, supply a key:

```sh
TYPESAFE_API_KEY=... dotnet test tests/TypeSafe.Sdk.IntegrationTests
```

CI builds and runs the project like any other, and the three tests skip there: no key is configured as
a GitHub secret, and none is planned. The key is read from the environment only; it is never logged,
echoed, or put in an assertion message, and the bad-key test mints its own invalid credential rather
than sending yours on a request expected to fail. Of the three, only the bad-key test has actually been
observed against the live API — the two that need a valid key have never run, so treat the first keyed
run as the verification.

The version is written in three places and they must match: `<Version>` in
`src/TypeSafe.Sdk/TypeSafe.Sdk.csproj`, `TypeSafeConstants.Version` in
`src/TypeSafe.Sdk/Constants.cs`, and `<Version>` in
`src/TypeSafe.Sdk.DependencyInjection/TypeSafe.Sdk.DependencyInjection.csproj` — the two packages
ship in lockstep. `scripts/check-version.sh` fails CI if they drift apart; run it locally after a
version bump, optionally with the expected version (`scripts/check-version.sh 0.7.0`) to assert all
three files are at that value.

## Differences from the Python and JavaScript SDKs

- The API is async only (`SystemOneAsync`, `Models.ListAsync`) with `CancellationToken` support; there is no synchronous client.
- Answer types come from the question object rather than from the question map. `Question.Named.Noul("billing", ...)` returns a `Named<NoulAnswer>`, and `response.Get(billing)` is a `NoulAnswer` at compile time — where the JavaScript SDK infers answer types from the shape of the object literal it was handed, so `response.answers.billing` stays typed only while the questions are a literal the compiler can see through. Here the types travel with the questions: through a field, a helper method, or a list built at run time. The C# equivalent of that inference is therefore the `Named<TAnswer>` value, not the map.
- The grouped views are kept as the Python SDK has them (`Nouls`, `Choices`, `Scores`, and `Answers`), keyed by the same names; they are how the dictionary form reads answers, and they stay available alongside `Get`. They keep the order the server sent, as a Python `dict` and a JavaScript object do, which a .NET `Dictionary` would not: `Answers` is an `OrderedDictionary<string, Answer>`, and `response.GetAt(index)` — which neither upstream SDK has — reads an answer by position. The three grouped views are extension members on `IReadOnlyDictionary<string, Answer>`, so any map of answers has them.
- The wire answer names are kept (`NoulAnswer.Noul`, `ChoiceAnswer.Choice`) and joined by the get-only aliases `NoulAnswer.Probability` and `ChoiceAnswer.Label`. The aliases are computed, so they add nothing to the wire format.
- `ListModelsResponse` implements `IReadOnlyList<ModelMetadata>`: the response is enumerated and indexed directly, where the Python and JavaScript SDKs read a `models` collection off it. There is no `Models` property.
- Caller cancellation surfaces as the standard `OperationCanceledException` rather than a dedicated abort error.
- `RetryPolicy.TotalTimeout` (the Python SDK's retry budget) is available but disabled by default, matching the JavaScript SDK.
- `TypeSafeApiConnectionException.Error` carries the `System.Net.Http.HttpRequestError` the failed request reported (`NameResolutionError`, `ConnectionError`, `SecureConnectionError`, …), or `null` when the failure came from elsewhere — an interrupted body read or a timeout. The Python and JavaScript SDKs report no equivalent; a `RetryWhen` predicate can match on it.
- `RetryPolicy`'s retry switches are named as actions (`RetryConnectionErrors`, `RetryTimeouts`, `RetryWhen`) rather than after the upstream error types; the behaviour they control is unchanged.
- HTTP status codes are typed: responses and API exceptions expose `StatusCode` as a `System.Net.HttpStatusCode` instead of the upstream integer `status`. Exception messages still carry the numeric code, and `RetryPolicy.HttpStatuses` remains a set of `int`.
- `TypeSafeClientOptions`, `RequestOptions`, and `RetryPolicy` are `sealed record`s set once through `init` setters, where the Python and JavaScript SDKs take mutable option objects or keyword arguments. Being records, they compare by value and derive variants with `with` (`client.Retry with { MaxRetries = 0 }`); reference-typed members — `HttpClient`, `Logger`, `TimeProvider`, and the header collections — compare by reference, so two option sets holding equal-but-distinct header dictionaries are not equal. `TypeSafeClientOptions.ToString()` is overridden rather than compiler-generated, so neither the `ApiKey` nor `DefaultHeaders` is ever printed.
- The request state and question instructions and descriptions are a typed `Content` value rather than the untyped values the Python and JavaScript SDKs accept: `string`, `JsonObject`, and `JsonArray` convert implicitly, `Content.Null` leaves a value unset, and any other object is passed explicitly through `Content.From(obj)` or `Content.From(value, typeInfo)`. This covers `SystemOneAsync(state, ...)` and `SystemOneRequest.State` as well as the `Question` builders. The wire format is unchanged.
- A supplied `HttpClient` is not disposed with the client unless `DisposeHttpClient` is set.
- An answer whose `type` this SDK version does not model is kept, not skipped: it arrives in `Answers` as a bare `Answer` whose `AdditionalProperties` holds the whole payload, the `type` discriminator included, and it is excluded from `Nouls`, `Choices` and `Scores`. A warning is still logged for it (`Ignoring answer "<name>" with unrecognized type "<type>"`), matching the Python SDK's message, but the Python SDK discards the answer where this SDK leaves it inspectable — through `AdditionalProperties` or the buffered `RawHttpResponse`.
- `TypeSafeApiResponseValidationException.FieldPath` is the JSON path the reader was on, with the leading `$.` removed: `answers.tone.confidence` for a value of the wrong type, `models[0]` for a list position, `answers.tone` for an answer missing a required member (the reader blames the object it could not build), and `""` for the whole body. The Python and JavaScript SDKs report their validator's own path for the same defect.
- The name-first question builders are `Question.Named.Noul("billing", "Is this a billing issue?")`, `Question.Named.Choice(...)` and `Question.Named.Score(...)`; they return a `Named<TAnswer>` pairing the name with the typed question (and implementing the non-generic `INamedQuestion`), where the Python and JavaScript SDKs only key questions by dictionary entry. They sit on `Question.Named` rather than overloading the positional builders on purpose: a string binds to a `string name` parameter by identity conversion, which beats the implicit conversion to `Content`, so an overload would silently turn `Question.Choice("Tone?", "calm", "angry")` into a name plus one label. The positional builders keep their meaning and the wire format is unchanged.
- There is no `Questions` collection type. The dictionary-based `SystemOneAsync(state, questions, ...)` overload and `SystemOneRequest` take any `IEnumerable<KeyValuePair<string, Question>>`, so a `Dictionary<string, Question>` — or any other sequence of name/question pairs — is passed directly, where the Python and JavaScript SDKs take a plain object or dict. An earlier release wrapped this in a `Questions : Dictionary<string, Question>` class; it was removed because it added a second spelling for a dictionary without adding behaviour. The wire format is unchanged.
- The package targets `net10.0` only, where the Python and JavaScript SDKs support a range of
  runtimes. .NET 8 consumers must stay on 0.6.0, the last `net8.0` release.

## Documentation

Learn what TypeSafe can do in the [TypeSafe docs](https://docs.typesafe.ai/).
