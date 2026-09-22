using Microsoft.Extensions.Logging;

namespace TypeSafe.DependencyInjection;

/// <summary>
/// The configuration-bindable subset of <see cref="TypeSafeClientOptions"/>, bound from the
/// <c>TypeSafe</c> configuration section. It is a mutable class with only bindable property types so
/// the configuration binding source generator can bind it without reflection; <see cref="ToOptions"/>
/// maps it onto the init-only <see cref="TypeSafeClientOptions"/> record the client takes.
/// Unset values stay <c>null</c> so <see cref="TypeSafeClient"/> keeps resolving <c>TYPESAFE_*</c>
/// environment variables and SDK defaults for them.
/// </summary>
internal sealed class TypeSafeClientSettings
{
    /// <summary>API key; unset falls back to <c>TYPESAFE_API_KEY</c>.</summary>
    public string? ApiKey { get; set; }

    /// <summary>API root; unset falls back to <c>TYPESAFE_BASE_URL</c>, then the SDK default.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Default model; unset falls back to <c>TYPESAFE_DEFAULT_MODEL</c>, then the SDK default.</summary>
    public string? DefaultModel { get; set; }

    /// <summary>Timeout per attempt; unset uses <see cref="TypeSafeConstants.DefaultTimeout"/>.</summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// SDK-side minimum log level, written with the <see cref="Microsoft.Extensions.Logging.LogLevel"/>
    /// names (<c>Debug</c>, <c>Information</c>, <c>Warning</c>, <c>Error</c>, <c>None</c>) rather than the
    /// <c>TYPESAFE_LOG_LEVEL</c> spellings. Unset falls back to <c>TYPESAFE_LOG_LEVEL</c>, then <c>Warning</c>.
    /// </summary>
    public LogLevel? LogLevel { get; set; }

    /// <summary>Headers sent with every request.</summary>
    public Dictionary<string, string>? DefaultHeaders { get; set; }

    /// <summary>Retry overrides applied over <see cref="RetryPolicy.Default"/>.</summary>
    public TypeSafeRetrySettings? Retry { get; set; }

    /// <summary>Map these settings onto client options, leaving unset values <c>null</c> for the client to resolve.</summary>
    public TypeSafeClientOptions ToOptions() => new()
    {
        ApiKey = ApiKey,
        BaseUrl = BaseUrl,
        DefaultModel = DefaultModel,
        // The named HttpClient's own timeout is disabled, so the per-attempt default has to be explicit here.
        Timeout = Timeout ?? TypeSafeConstants.DefaultTimeout,
        LogLevel = LogLevel,
        DefaultHeaders = DefaultHeaders,
        Retry = Retry?.ToPolicy(),
    };
}

/// <summary>The configuration-bindable subset of <see cref="RetryPolicy"/>. Unset values keep the <see cref="RetryPolicy.Default"/> value.</summary>
internal sealed class TypeSafeRetrySettings
{
    /// <summary>Retries after the first attempt.</summary>
    public int? MaxRetries { get; set; }

    /// <summary>Delay before the first retry.</summary>
    public TimeSpan? BackoffInitial { get; set; }

    /// <summary>Upper bound on the backoff delay.</summary>
    public TimeSpan? BackoffMax { get; set; }

    /// <summary>Jitter fraction applied to each delay.</summary>
    public double? BackoffJitter { get; set; }

    /// <summary>HTTP status codes that are retried.</summary>
    public int[]? HttpStatuses { get; set; }

    /// <summary>Whether a <c>Retry-After</c> header is honoured.</summary>
    public bool? RespectRetryAfter { get; set; }

    /// <summary>Upper bound on an honoured <c>Retry-After</c> delay.</summary>
    public TimeSpan? MaxRetryAfter { get; set; }

    /// <summary>Whether connection errors are retried.</summary>
    public bool? RetryConnectionErrors { get; set; }

    /// <summary>Whether timeouts are retried.</summary>
    public bool? RetryTimeouts { get; set; }

    /// <summary>Budget across all attempts; unset leaves retries unbounded in total.</summary>
    public TimeSpan? TotalTimeout { get; set; }

    /// <summary>Apply the set values over <see cref="RetryPolicy.Default"/>.</summary>
    public RetryPolicy ToPolicy()
    {
        var policy = RetryPolicy.Default;
        if (MaxRetries is { } maxRetries) policy = policy with { MaxRetries = maxRetries };
        if (BackoffInitial is { } initial) policy = policy with { BackoffInitial = initial };
        if (BackoffMax is { } max) policy = policy with { BackoffMax = max };
        if (BackoffJitter is { } jitter) policy = policy with { BackoffJitter = jitter };
        if (HttpStatuses is { Length: > 0 } statuses) policy = policy with { HttpStatuses = new HashSet<int>(statuses) };
        if (RespectRetryAfter is { } respect) policy = policy with { RespectRetryAfter = respect };
        if (MaxRetryAfter is { } maxRetryAfter) policy = policy with { MaxRetryAfter = maxRetryAfter };
        if (RetryConnectionErrors is { } connection) policy = policy with { RetryConnectionErrors = connection };
        if (RetryTimeouts is { } timeouts) policy = policy with { RetryTimeouts = timeouts };
        if (TotalTimeout is { } total) policy = policy with { TotalTimeout = total };
        return policy;
    }
}
