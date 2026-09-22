using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;

// The tests read and write process environment variables, so classes must not run concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TypeSafe.DependencyInjection.Tests;

/// <summary>An <see cref="HttpMessageHandler"/> that records requests and answers with a canned response.</summary>
internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public StubHandler(Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
    {
        _respond = respond ?? (_ => Json(200, ModelsBody));
    }

    public List<Uri> Requests { get; } = new();

    public static HttpResponseMessage Json(int status, string json) => new((HttpStatusCode)status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    public const string ModelsBody = """
        {
          "models": [
            { "name": "jev-latest", "description": "Latest model", "release_date": "2026-09-01" },
            { "name": "jev-1", "description": "First model", "release_date": "2026-01-01" }
          ]
        }
        """;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request.RequestUri!);
        var response = _respond(request);
        response.RequestMessage ??= request;
        return Task.FromResult(response);
    }
}

/// <summary>A <see cref="DelegatingHandler"/> that counts the requests passing through it.</summary>
internal sealed class CountingHandler : DelegatingHandler
{
    private readonly Counter _counter;

    public CountingHandler(Counter counter)
    {
        _counter = counter;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _counter.Count++;
        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>A counter shared with the handler instances an <see cref="IHttpClientFactory"/> creates.</summary>
internal sealed class Counter
{
    public int Count { get; set; }
}

/// <summary>An <see cref="IHttpClientFactory"/> that hands out recorded clients, so a test can use one after the SDK client is disposed.</summary>
internal sealed class RecordingHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public RecordingHttpClientFactory(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public List<string> Names { get; } = new();

    public List<HttpClient> Created { get; } = new();

    public HttpClient CreateClient(string name)
    {
        Names.Add(name);
        // disposeHandler: false mirrors IHttpClientFactory, whose handler chain outlives the HttpClient.
        var client = new HttpClient(_handler, disposeHandler: false);
        Created.Add(client);
        return client;
    }
}

/// <summary>An <see cref="ILoggerFactory"/> that always returns the same logger, so a test can assert the client uses it.</summary>
internal sealed class StubLoggerFactory : ILoggerFactory
{
    public StubLoggerFactory(ILogger logger)
    {
        Logger = logger;
    }

    public ILogger Logger { get; }

    public List<string> Categories { get; } = new();

    public ILogger CreateLogger(string categoryName)
    {
        Categories.Add(categoryName);
        return Logger;
    }

    public void AddProvider(ILoggerProvider provider) { }

    public void Dispose() { }
}

/// <summary>A logger that discards everything it is given.</summary>
internal sealed class NullLogger : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
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
