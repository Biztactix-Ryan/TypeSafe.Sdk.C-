namespace TypeSafe.IntegrationTests;

/// <summary>
/// The environment the live tests run against, read once per test run: <c>TYPESAFE_API_KEY</c> and the
/// optional <c>TYPESAFE_BASE_URL</c>. The key is never logged, echoed, or included in an assertion
/// message; only whether it is present is ever observable.
/// </summary>
/// <remarks>
/// <para>
/// Every test in this project calls <see cref="SkipUnlessConfigured"/> first, so a run without a key
/// reports the tests as skipped with a reason instead of failing. CI carries no key by design.
/// </para>
/// <para>
/// The passing path is largely unverified: no API key was available when these tests were written, so
/// the two tests that need one — the model listing and the System One call — are written to pass
/// against the live API but have never been observed doing so. Treat the first run with a real key as
/// the verification. The bad-key test needs no credential and has been run against the live API.
/// </para>
/// </remarks>
internal static class LiveApiFixture
{
    /// <summary>The reason reported by every skipped live test.</summary>
    internal const string SkipReason = "TYPESAFE_API_KEY is not set; live tests skipped";

    private static readonly string? ApiKey = Read("TYPESAFE_API_KEY");

    private static readonly string? BaseUrl = Read("TYPESAFE_BASE_URL");

    /// <summary>
    /// Generous per-attempt timeout: a live System One call does real work, and the SDK's ten-second
    /// default is tuned for unit tests rather than for a model round trip.
    /// </summary>
    private static readonly TimeSpan LiveTimeout = TimeSpan.FromSeconds(60);

    /// <summary>Skip the calling test, with a visible reason, unless an API key is configured.</summary>
    internal static void SkipUnlessConfigured() =>
        Skip.If(string.IsNullOrWhiteSpace(ApiKey), SkipReason);

    /// <summary>
    /// A client authenticated with the configured key. Call <see cref="SkipUnlessConfigured"/> first:
    /// the key is assumed present here.
    /// </summary>
    internal static TypeSafeClient CreateClient() => new(new TypeSafeClientOptions
    {
        ApiKey = ApiKey,
        BaseUrl = BaseUrl,
        Timeout = LiveTimeout,
    });

    /// <summary>
    /// A client carrying a key that cannot be valid, for the rejection test. The configured key is
    /// deliberately not reused, so a real credential is never sent on a request expected to fail.
    /// </summary>
    internal static TypeSafeClient CreateClientWithBadKey() => new(new TypeSafeClientOptions
    {
        ApiKey = "invalid-" + Guid.NewGuid().ToString("N"),
        BaseUrl = BaseUrl,
        Timeout = LiveTimeout,
        Retry = RetryPolicy.None,
    });

    /// <summary>
    /// A token source that bounds the whole test, so a hung live call fails the run rather than
    /// blocking it. Separate from the SDK's per-attempt <see cref="LiveTimeout"/>, which covers a
    /// single HTTP attempt and leaves retries their own budget.
    /// </summary>
    internal static CancellationTokenSource CreateTimeout() => new(TimeSpan.FromMinutes(2));

    private static string? Read(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
