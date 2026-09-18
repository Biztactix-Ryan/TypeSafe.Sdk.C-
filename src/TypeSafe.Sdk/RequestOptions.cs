namespace TypeSafe;

/// <summary>Per-call options that override client settings for a single request.</summary>
public sealed class RequestOptions
{
    /// <summary>Timeout per attempt; <c>null</c> inherits the client setting. There is no total budget unless <see cref="RetryPolicy.TotalTimeout"/> is set.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Retry policy for this call; <c>null</c> inherits the client setting. Derive one with <c>client.Retry with { ... }</c>.</summary>
    public RetryPolicy? Retry { get; init; }

    /// <summary>Additional headers, merged case-insensitively over the client's default headers. Authentication, SDK identification, <c>Accept</c>, and <c>Content-Type</c> remain protected.</summary>
    public IEnumerable<KeyValuePair<string, string>>? Headers { get; init; }
}
