using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

// Several tests read and write process environment variables, so classes must not run concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TypeSafe.Tests;

/// <summary>A request captured by <see cref="StubHandler"/>, with its headers flattened and body read.</summary>
internal sealed record RecordedRequest(HttpMethod Method, Uri Url, IReadOnlyDictionary<string, string> Headers, string? Body)
{
    public JsonNode? Json => Body is null ? null : JsonNode.Parse(Body);

    public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
}

/// <summary>An <see cref="HttpMessageHandler"/> that records requests and answers from a callback.</summary>
internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<RecordedRequest, int, CancellationToken, Task<HttpResponseMessage>> _respond;

    public StubHandler(Func<RecordedRequest, int, HttpResponseMessage> respond)
        : this((request, attempt, _) => Task.FromResult(respond(request, attempt))) { }

    public StubHandler(Func<RecordedRequest, int, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        _respond = respond;
    }

    public List<RecordedRequest> Requests { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, values) in request.Headers) headers[name] = string.Join(", ", values);
        if (request.Content is not null)
        {
            foreach (var (name, values) in request.Content.Headers) headers[name] = string.Join(", ", values);
        }
        var recorded = new RecordedRequest(request.Method, request.RequestUri!, headers, body);
        var attempt = Requests.Count;
        Requests.Add(recorded);
        var response = await _respond(recorded, attempt, cancellationToken);
        // Real handlers hand the request back on the response; response-side failures read the endpoint off it.
        response.RequestMessage ??= request;
        return response;
    }
}

internal static class Http
{
    public static HttpResponseMessage Json(int status, string json, params (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        foreach (var (name, value) in headers) response.Headers.TryAddWithoutValidation(name, value);
        return response;
    }

    public static HttpResponseMessage Text(int status, string text, params (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent(text, Encoding.UTF8, "text/plain"),
        };
        foreach (var (name, value) in headers) response.Headers.TryAddWithoutValidation(name, value);
        return response;
    }

    public static HttpResponseMessage Empty(int status) => new((HttpStatusCode)status);

    public const string SystemOneBody = """
        {
          "model": "jev-latest",
          "usage": { "input_tokens": 120, "output_tokens": 8 },
          "answers": {
            "billing": { "type": "noul", "noul": 0.93 },
            "tone": {
              "type": "choice",
              "choice": "frustrated",
              "confidence": 0.81,
              "probabilities": { "calm": 0.05, "frustrated": 0.81, "angry": 0.14 }
            },
            "urgency": {
              "type": "score",
              "score": 2.4,
              "confidence": 0.7,
              "legend": { "0": "can wait", "1": "this week", "2": "today", "3": { "label": "right now" } },
              "probabilities": { "0": 0.0, "1": 0.1, "2": 0.4, "3": 0.5 }
            }
          }
        }
        """;

    public const string ModelsBody = """
        {
          "models": [
            { "name": "jev-latest", "description": "Latest model", "release_date": "2026-09-01" },
            { "name": "jev-1", "description": "First model", "release_date": "2026-01-01" }
          ]
        }
        """;
}

/// <summary>Collects SDK log lines for assertions.</summary>
internal sealed class CapturingLogger : ILogger
{
    public List<(LogLevel Level, string Message)> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));

    public IEnumerable<string> Messages(LogLevel level) => Entries.Where(e => e.Level == level).Select(e => e.Message);
}

internal static class Clients
{
    public const string ApiKey = "sk-test-key-1234567890";

    /// <summary>Fast backoff so retry tests don't sleep.</summary>
    public static readonly RetryPolicy FastRetry = RetryPolicy.Default with
    {
        BackoffInitial = TimeSpan.FromMilliseconds(1),
        BackoffMax = TimeSpan.FromMilliseconds(1),
    };

    /// <summary>
    /// Builds a stubbed client. <see cref="TypeSafeClientOptions"/> is init-only, so tweaks are
    /// collected on a mutable <see cref="ClientSetup"/> first and applied in the object initializer.
    /// </summary>
    public static TypeSafeClient Create(StubHandler handler, Action<ClientSetup>? configure = null)
    {
        var setup = new ClientSetup { Retry = FastRetry };
        configure?.Invoke(setup);
        return new TypeSafeClient(new TypeSafeClientOptions
        {
            ApiKey = ApiKey,
            HttpMessageHandler = handler,
            BaseUrl = setup.BaseUrl,
            DefaultModel = setup.DefaultModel,
            DefaultHeaders = setup.DefaultHeaders,
            Timeout = setup.Timeout,
            Retry = setup.Retry,
            LogLevel = setup.LogLevel ?? LogLevel.None,
            Logger = setup.Logger,
        });
    }

    public static Dictionary<string, Question> SampleQuestions() => new()
    {
        ["billing"] = Question.Noul("Is this about billing?"),
        ["tone"] = Question.Choice("What is the tone?", "calm", "frustrated", "angry"),
        ["urgency"] = Question.Score("How urgent?", "can wait", "this week", "today", "right now"),
    };
}

/// <summary>Mutable stand-in for the init-only client options, for tests that tweak one or two values.</summary>
internal sealed class ClientSetup
{
    public string? BaseUrl { get; set; }

    public string? DefaultModel { get; set; }

    public IEnumerable<KeyValuePair<string, string>>? DefaultHeaders { get; set; }

    public TimeSpan? Timeout { get; set; }

    public RetryPolicy? Retry { get; set; }

    public LogLevel? LogLevel { get; set; }

    public ILogger? Logger { get; set; }
}

/// <summary>Sets environment variables for the duration of a test and restores them afterwards.</summary>
internal sealed class EnvScope : IDisposable
{
    private readonly Dictionary<string, string?> _previous = new();

    public EnvScope(params (string Name, string? Value)[] variables)
    {
        foreach (var (name, value) in variables)
        {
            _previous[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    /// <summary>Clear every TYPESAFE_* variable so tests start from a known state.</summary>
    public static EnvScope Clean(params (string Name, string? Value)[] variables) => new(
        new[]
        {
            (TypeSafeConstants.ApiKeyEnv, (string?)null),
            (TypeSafeConstants.BaseUrlEnv, null),
            (TypeSafeConstants.DefaultModelEnv, null),
            (TypeSafeConstants.LogLevelEnv, null),
        }.Concat(variables).ToArray());

    public void Dispose()
    {
        foreach (var (name, value) in _previous) Environment.SetEnvironmentVariable(name, value);
    }
}
