# TypeSafe AI C# SDK

C# SDK for [TypeSafe AI](https://typesafe.ai), ported from the official
[Python](https://github.com/typesafe-ai/typesafe-sdk-python) and
[JavaScript](https://github.com/typesafe-ai/typesafe-sdk-js) SDKs (v0.6.0).

Targets .NET 8 and later. The only dependency is `Microsoft.Extensions.Logging.Abstractions`.

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
    state: new { document = "I was charged twice. Please fix this ASAP." },
    questions: new Questions
    {
        ["category"] = Question.Choice("What is this ticket about?", "billing", "technical", "other"),
    });

Console.WriteLine(response.Choices["category"].Choice);
```

Answers are grouped by question type: `response.Nouls`, `response.Choices`, and
`response.Scores`, or all together in `response.Answers`.

## Questions

Build questions with the static factories on `Question`. Instructions and descriptions accept text,
a JSON object, or an array. Plain .NET objects (anonymous types, records, dictionaries) are serialized
with camelCase web defaults; pass a `JsonNode` to control serialization yourself.

```csharp
var questions = new Questions
{
    // Yes/no, with optional descriptions of either outcome.
    ["isBilling"] = Question.Noul("Is this ticket about billing?", whenTrue: "mentions charges or invoices"),

    // Select between labels, undescribed or described.
    ["tone"] = Question.Choice("What is the customer's tone?", "calm", "frustrated", "angry"),
    ["tier"] = Question.Choice("Which plan?", new Dictionary<string, string?>
    {
        ["free"] = "no payment on file",
        ["pro"] = null,
    }),

    // Ordered rubric, one description per score from zero.
    ["urgency"] = Question.Score("How urgent is this?", "can wait", "this week", "today", "right now"),

    // Raw JSON for question types or fields this SDK version does not model.
    ["future"] = Question.FromJson(new JsonObject { ["type"] = "noul", ["instructions"] = "Really?", ["extra"] = 1 }),
};
```

## Responses

```csharp
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

```csharp
var models = await client.Models.ListAsync();
foreach (var model in models.Models)
    Console.WriteLine($"{model.Name}: {model.Description} ({model.ReleaseDate})");
```

## Configuration

Explicit options take precedence over environment variables, then SDK defaults. Empty or
whitespace-only environment values are ignored.

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
HTTP 408, 429, and 5xx responses, connection errors, and timeouts. `Predicate` adds custom rules,
`TotalTimeout` caps the whole call, and `RetryPolicy.None` disables retries.

### Logging

The SDK logs through `Microsoft.Extensions.Logging.ILogger`, defaulting to a `[typesafe-sdk]`
prefixed logger on standard error. `info` logs request summaries; `debug` adds headers and bodies.
Credential headers are redacted; bodies are not.

## Errors

| Exception                                | When                                                     |
| ---------------------------------------- | -------------------------------------------------------- |
| `TypeSafeException`                      | Base type; also invalid configuration or questions       |
| `TypeSafeApiException`                   | Any non-2xx response: `Status`, `Body`, `Headers`, `RequestId` |
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
    Console.Error.WriteLine($"{error.Status} {error.Detail} (request {error.RequestId})");
}
```

## Testing your code

`ITypeSafeClient` and `IModelsResource` are provided for mocking, and every response and answer type
has a public constructor. To exercise the real client without the network, pass an
`HttpMessageHandler` through `TypeSafeClientOptions.HttpMessageHandler`.

## Development

```sh
dotnet build
dotnet test
TYPESAFE_API_KEY=... dotnet run --project examples/TypeSafe.Sdk.Demo
```

## Differences from the Python and JavaScript SDKs

- The API is async only (`SystemOneAsync`, `Models.ListAsync`) with `CancellationToken` support; there is no synchronous client.
- Answers are grouped like the Python SDK (`Nouls`, `Choices`, `Scores`). The JavaScript SDK's compile-time inference of answer types from question keys has no C# equivalent.
- Caller cancellation surfaces as the standard `OperationCanceledException` rather than a dedicated abort error.
- `RetryPolicy.TotalTimeout` (the Python SDK's retry budget) is available but disabled by default, matching the JavaScript SDK.
- A supplied `HttpClient` is not disposed with the client unless `DisposeHttpClient` is set.

## Documentation

Learn what TypeSafe can do in the [TypeSafe docs](https://docs.typesafe.ai/).
