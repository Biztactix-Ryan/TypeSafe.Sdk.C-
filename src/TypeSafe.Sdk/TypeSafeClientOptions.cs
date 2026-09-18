using Microsoft.Extensions.Logging;

namespace TypeSafe;

/// <summary>
/// Client options. Explicit values take precedence over environment variables, then SDK defaults.
/// Empty or whitespace-only environment values are ignored.
/// </summary>
public sealed class TypeSafeClientOptions
{
    /// <summary>Required API key; falls back to <c>TYPESAFE_API_KEY</c>.</summary>
    public string? ApiKey { get; set; }

    /// <summary>API root; falls back to <c>TYPESAFE_BASE_URL</c>, then <c>https://api.typesafe.ai</c>.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Default model; falls back to <c>TYPESAFE_DEFAULT_MODEL</c>, then <c>jev-latest</c>.</summary>
    public string? DefaultModel { get; set; }

    /// <summary>
    /// Timeout per attempt, with no total retry budget. Default: 10 seconds, or the supplied
    /// <see cref="HttpClient"/>'s timeout when one is provided. <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> disables it.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>Retry policy; <c>null</c> uses <see cref="RetryPolicy.Default"/>. Pass <see cref="RetryPolicy.None"/> to disable retries.</summary>
    public RetryPolicy? Retry { get; set; }

    /// <summary>Additional request headers sent with every request; per-call headers take precedence.</summary>
    public IEnumerable<KeyValuePair<string, string>>? DefaultHeaders { get; set; }

    /// <summary>
    /// An existing <see cref="System.Net.Http.HttpClient"/> to send requests with. Mutually exclusive with
    /// <see cref="HttpMessageHandler"/>. The SDK does not dispose it unless <see cref="DisposeHttpClient"/> is set.
    /// </summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>A custom handler for the SDK-owned <see cref="System.Net.Http.HttpClient"/>, useful for tests and transport configuration. Mutually exclusive with <see cref="HttpClient"/>.</summary>
    public HttpMessageHandler? HttpMessageHandler { get; set; }

    /// <summary>Whether disposing the client also disposes a supplied <see cref="HttpClient"/>. Default: false.</summary>
    public bool DisposeHttpClient { get; set; }

    /// <summary>
    /// Minimum log level; falls back to <c>TYPESAFE_LOG_LEVEL</c> (<c>debug</c>, <c>info</c>, <c>warn</c>, <c>error</c>, <c>off</c>), then <c>warn</c>.
    /// <c>info</c> logs request summaries; <c>debug</c> adds headers and bodies. Known credential headers are redacted; bodies are not.
    /// </summary>
    public LogLevel? LogLevel { get; set; }

    /// <summary>Logger receiving SDK messages at <see cref="LogLevel"/> and above. Default: a prefixed logger writing to standard error.</summary>
    public ILogger? Logger { get; set; }
}
