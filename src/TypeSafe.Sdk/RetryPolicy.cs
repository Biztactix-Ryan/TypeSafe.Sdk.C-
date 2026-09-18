using System.Collections.Frozen;
using TypeSafe.Internal;

namespace TypeSafe;

/// <summary>
/// Configuration for SDK retry behavior. Use <c>with</c> expressions to derive a policy from
/// <see cref="Default"/> or from <see cref="TypeSafeClient.Retry"/>.
/// </summary>
/// <example>
/// <code>
/// var client = new TypeSafeClient(new TypeSafeClientOptions
/// {
///     Retry = RetryPolicy.Default with { MaxRetries = 3, HttpStatuses = new HashSet&lt;int&gt; { 429, 500, 502, 503, 504 } },
/// });
/// </code>
/// </example>
public sealed record RetryPolicy
{
    /// <summary>HTTP 408, 429, and every 5xx status.</summary>
    public static readonly IReadOnlySet<int> DefaultHttpStatuses =
        new[] { 408, 429 }.Concat(Enumerable.Range(500, 100)).ToFrozenSet();

    /// <summary>The SDK's default retry policy.</summary>
    public static RetryPolicy Default { get; } = new();

    /// <summary>A policy that never retries.</summary>
    public static RetryPolicy None { get; } = new() { MaxRetries = 0 };

    /// <summary>Maximum retries after the initial attempt; <c>0</c> disables retries. Default: 2.</summary>
    public int MaxRetries { get; init; } = 2;

    /// <summary>First backoff delay, doubled each attempt up to <see cref="BackoffMax"/>. Default: 500ms.</summary>
    public TimeSpan BackoffInitial { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Maximum backoff delay. Default: 5s.</summary>
    public TimeSpan BackoffMax { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Fraction of each backoff delay randomly subtracted, from 0 to 1. Default: 0.25.</summary>
    public double BackoffJitter { get; init; } = 0.25;

    /// <summary>HTTP status codes that are retried. Default: 408, 429, and 500–599.</summary>
    public IReadOnlySet<int> HttpStatuses { get; init; } = DefaultHttpStatuses;

    /// <summary>Whether to honor <c>Retry-After</c> and <c>retry-after-ms</c> response headers. Default: true.</summary>
    public bool RespectRetryAfter { get; init; } = true;

    /// <summary>Maximum server-requested delay honored; longer delays fall back to backoff. Default: 60s.</summary>
    public TimeSpan MaxRetryAfter { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Whether to retry <see cref="TypeSafeApiConnectionException"/>, including interrupted response bodies. Default: true.</summary>
    public bool RetryConnectionErrors { get; init; } = true;

    /// <summary>Whether to retry <see cref="TypeSafeApiTimeoutException"/>. Default: true.</summary>
    public bool RetryTimeouts { get; init; } = true;

    /// <summary>An optional predicate called with the raised exception; returning <c>true</c> triggers a retry in addition to the other rules.</summary>
    public Func<Exception, bool>? RetryWhen { get; init; }

    /// <summary>
    /// Total retry budget per SDK call, including the initial attempt and delays; <c>null</c> disables the limit.
    /// The SDK stops before a retry whose delay would reach or exceed the budget and rethrows the last error. Default: null.
    /// </summary>
    public TimeSpan? TotalTimeout { get; init; }

    /// <summary>Validate the policy, throwing <see cref="TypeSafeException"/> for invalid values.</summary>
    public RetryPolicy Validate()
    {
        if (MaxRetries < 0)
            throw new TypeSafeException($"`{nameof(MaxRetries)}` must be a non-negative integer, got {MaxRetries}.");
        if (BackoffInitial < TimeSpan.Zero)
            throw new TypeSafeException($"`{nameof(BackoffInitial)}` must be a non-negative duration, got {BackoffInitial}.");
        if (BackoffMax < TimeSpan.Zero)
            throw new TypeSafeException($"`{nameof(BackoffMax)}` must be a non-negative duration, got {BackoffMax}.");
        if (double.IsNaN(BackoffJitter) || BackoffJitter < 0 || BackoffJitter > 1)
            throw new TypeSafeException($"`{nameof(BackoffJitter)}` must be between 0 and 1, got {BackoffJitter}.");
        if (HttpStatuses is null)
            throw new TypeSafeException($"`{nameof(HttpStatuses)}` must not be null.");
        foreach (var status in HttpStatuses)
        {
            if (status < 100 || status > 999)
                throw new TypeSafeException($"`{nameof(HttpStatuses)}` must contain HTTP status codes, got {status}.");
        }
        if (MaxRetryAfter < TimeSpan.Zero)
            throw new TypeSafeException($"`{nameof(MaxRetryAfter)}` must be a non-negative duration, got {MaxRetryAfter}.");
        if (TotalTimeout is { } total && total <= TimeSpan.Zero)
            throw new TypeSafeException($"`{nameof(TotalTimeout)}` must be a positive duration, got {total}.");
        return this;
    }

    /// <summary>Whether the policy retries the given failure.</summary>
    public bool ShouldRetry(Exception error)
    {
        var builtin = error switch
        {
            TypeSafeApiTimeoutException => RetryTimeouts,
            TypeSafeApiConnectionException => RetryConnectionErrors,
            TypeSafeApiException api => HttpStatuses.Contains((int)api.StatusCode),
            _ => false,
        };
        return builtin || (RetryWhen is not null && RetryWhen(error));
    }

    /// <summary>
    /// Calculate the delay before a zero-based retry attempt: an allowed server delay from the
    /// response headers, otherwise capped exponential backoff with jitter.
    /// </summary>
    public TimeSpan DelayFor(int attempt, IReadOnlyDictionary<string, string>? headers = null, Random? random = null)
    {
        if (RespectRetryAfter && headers is not null)
        {
            var retryAfter = RetryAfterParser.Parse(headers);
            if (retryAfter is { } serverDelay && serverDelay <= MaxRetryAfter) return serverDelay;
        }
        var exponential = Math.Min(BackoffInitial.TotalMilliseconds * Math.Pow(2, attempt), BackoffMax.TotalMilliseconds);
        var jittered = exponential * (1 - (random ?? Random.Shared).NextDouble() * BackoffJitter);
        return TimeSpan.FromMilliseconds(Math.Round(jittered));
    }
}
