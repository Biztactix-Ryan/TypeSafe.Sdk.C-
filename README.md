# TypeSafe AI C# SDK

C# SDK for [TypeSafe AI](https://typesafe.ai), ported from the official
[Python](https://github.com/typesafe-ai/typesafe-sdk-python) and
[JavaScript](https://github.com/typesafe-ai/typesafe-sdk-js) SDKs (v0.6.0).

## Requirements

- Targets `net10.0`, so consuming projects need the .NET 10 runtime (and the .NET 10 SDK to build
  from source). Earlier targets are not multi-targeted.
- The only dependency is `Microsoft.Extensions.Logging.Abstractions`.
- The library is built with `IsAotCompatible`, so it is marked trimmable and ships clean under
  `PublishTrimmed` and `PublishAot`. All JSON goes through a source-generated serializer context; the
  one reflection path is the `Content.From(object?)` overload described under
  [Content](#content), which is annotated so your call site — not the runtime — tells you about it.

If you are still on .NET 8, stay on version 0.6.0 — it is the last release that targets `net8.0`.

## About this repository

I wrote this repository while waiting to be given access to TypeSafe AI. It is an unofficial,
community port: the public API, retry behaviour, error mapping, and header handling are translated
directly from the official Python and JavaScript SDKs at version 0.6.0, so it should behave the
same way. Until I have API access, the test suite runs against a stubbed HTTP handler rather than
the live service, so treat the first real requests as a shakedown and please open an issue if the
wire format has drifted.

Ryan, [Biztactix](https://biztactix.com.au)

## Quickstart

Add a reference to the `TypeSafe.Sdk` project (or the package once published), set
`TYPESAFE_API_KEY` in your environment, then create and use the client:

```csharp
using TypeSafe;

using var client = new TypeSafeClient();

var response = await client.SystemOneAsync(
    state: Content.From(new { document = "I was charged twice. Please fix this ASAP." }),
    questions: new Questions
    {
        ["category"] = Question.Choice("What is this ticket about?", "billing", "technical", "other"),
    });

Console.WriteLine(response.Choices["category"].Choice);
```

Answers are grouped by question type: `response.Nouls`, `response.Choices`, and
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

Build questions with the static factories on `Question`. Instructions and descriptions are
[`Content`](#content), so text, a `JsonObject`, and a `JsonArray` convert implicitly and
`Content.Null` leaves one unset.

```csharp
var questions = new Questions
{
    // Yes/no, with optional descriptions of either outcome.
    ["isBilling"] = Question.Noul("Is this ticket about billing?", whenTrue: "mentions charges or invoices"),

    // Select between labels, undescribed or described.
    ["tone"] = Question.Choice("What is the customer's tone?", "calm", "frustrated", "angry"),
    ["tier"] = Question.Choice("Which plan?", new Dictionary<string, Content>
    {
        ["free"] = "no payment on file",
        ["pro"] = Content.Null,
    }),

    // Ordered rubric, one description per score from zero.
    ["urgency"] = Question.Score("How urgent is this?", "can wait", "this week", "today", "right now"),

    // Raw JSON for question types or fields this SDK version does not model.
    ["future"] = Question.FromJson(new JsonObject { ["type"] = "noul", ["instructions"] = "Really?", ["extra"] = 1 }),
};
```

## Responses

```csharp
var billing = response.Nouls["isBilling"];
billing.Noul;             // 0.93
billing.Probability;      // the same value, under a .NET-style name

var tone = response.Choices["tone"];
tone.Choice;              // "frustrated"
tone.Label;               // the same value, under a .NET-style name
tone.Confidence;
tone.Probabilities["angry"];

var urgency = response.Scores["urgency"];
urgency.Score;            // expected score, e.g. 2.4
urgency.Confidence;
urgency.Legend[2];        // "today" (JsonNode)
urgency.Probabilities[3]; // 0.5

response.Model;
response.Usage.InputTokens;
response.RequestId;       // x-typesafe-request-id, or null
response.RawHttpResponse; // the buffered HttpResponseMessage
```

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
whitespace-only environment values are ignored. `TypeSafeClientOptions` properties are `init`-only,
so set them in the object initializer; the client reads them once, while it is being constructed.

| Option           | Environment variable     | Default                   |
| ---------------- | ------------------------ | ------------------------- |
| `ApiKey`         | `TYPESAFE_API_KEY`       | required                  |
| `BaseUrl`        | `TYPESAFE_BASE_URL`      | `https://api.typesafe.ai` |
| `DefaultModel`   | `TYPESAFE_DEFAULT_MODEL` | `jev-latest`              |
| `LogLevel`       | `TYPESAFE_LOG_LEVEL`     | `warn`                    |
| `Timeout`        |                          | 10 seconds per attempt    |
| `Retry`          |                          | `RetryPolicy.Default`     |
| `DefaultHeaders` |                          | none                      |

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

Per-call overrides go in `RequestOptions`:

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
`TotalTimeout` caps the whole call, and `RetryPolicy.None` disables retries.

### Logging

The SDK logs through `Microsoft.Extensions.Logging.ILogger`, defaulting to a `[typesafe-sdk]`
prefixed logger on standard error. `info` logs request summaries; `debug` adds headers and bodies.
Credential headers are redacted; bodies are not.

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
| `TypeSafeApiConnectionException`         | No HTTP response: DNS, TLS, reset, interrupted body      |
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

## Differences from the Python and JavaScript SDKs

- The API is async only (`SystemOneAsync`, `Models.ListAsync`) with `CancellationToken` support; there is no synchronous client.
- Answers are grouped like the Python SDK (`Nouls`, `Choices`, `Scores`). The JavaScript SDK's compile-time inference of answer types from question keys has no C# equivalent.
- The wire answer names are kept (`NoulAnswer.Noul`, `ChoiceAnswer.Choice`) and joined by the get-only aliases `NoulAnswer.Probability` and `ChoiceAnswer.Label`. The aliases are computed, so they add nothing to the wire format.
- `ListModelsResponse` implements `IReadOnlyList<ModelMetadata>`: the response is enumerated and indexed directly, where the Python and JavaScript SDKs read a `models` collection off it. There is no `Models` property.
- Caller cancellation surfaces as the standard `OperationCanceledException` rather than a dedicated abort error.
- `RetryPolicy.TotalTimeout` (the Python SDK's retry budget) is available but disabled by default, matching the JavaScript SDK.
- `RetryPolicy`'s retry switches are named as actions (`RetryConnectionErrors`, `RetryTimeouts`, `RetryWhen`) rather than after the upstream error types; the behaviour they control is unchanged.
- HTTP status codes are typed: responses and API exceptions expose `StatusCode` as a `System.Net.HttpStatusCode` instead of the upstream integer `status`. Exception messages still carry the numeric code, and `RetryPolicy.HttpStatuses` remains a set of `int`.
- `TypeSafeClientOptions`, `RequestOptions`, and `RetryPolicy` are set once through `init` setters, where the Python and JavaScript SDKs take mutable option objects or keyword arguments.
- The request state and question instructions and descriptions are a typed `Content` value rather than the untyped values the Python and JavaScript SDKs accept: `string`, `JsonObject`, and `JsonArray` convert implicitly, `Content.Null` leaves a value unset, and any other object is passed explicitly through `Content.From(obj)` or `Content.From(value, typeInfo)`. This covers `SystemOneAsync(state, ...)` and `SystemOneRequest.State` as well as the `Question` builders. The wire format is unchanged.
- A supplied `HttpClient` is not disposed with the client unless `DisposeHttpClient` is set.
- An answer whose `type` this SDK version does not model is kept, not skipped: it arrives in `Answers` as a bare `Answer` whose `AdditionalProperties` holds the whole payload, the `type` discriminator included, and it is excluded from `Nouls`, `Choices` and `Scores`. A warning is still logged for it (`Ignoring answer "<name>" with unrecognized type "<type>"`), matching the Python SDK's message, but the Python SDK discards the answer where this SDK leaves it inspectable — through `AdditionalProperties` or the buffered `RawHttpResponse`.
- `TypeSafeApiResponseValidationException.FieldPath` is the JSON path the reader was on, with the leading `$.` removed: `answers.tone.confidence` for a value of the wrong type, `models[0]` for a list position, `answers.tone` for an answer missing a required member (the reader blames the object it could not build), and `""` for the whole body. The Python and JavaScript SDKs report their validator's own path for the same defect.
- The package targets `net10.0` only, where the Python and JavaScript SDKs support a range of
  runtimes. .NET 8 consumers must stay on 0.6.0, the last `net8.0` release.

## Documentation

Learn what TypeSafe can do in the [TypeSafe docs](https://docs.typesafe.ai/).
