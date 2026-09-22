using System.Globalization;

namespace TypeSafe.Internal;

/// <summary>Parse <c>retry-after-ms</c> or <c>Retry-After</c> into a delay, preferring <c>retry-after-ms</c>.</summary>
internal static class RetryAfterParser
{
    /// <param name="headers">Response headers to read the delay from.</param>
    /// <param name="now">Explicit reference time for HTTP-date values; defaults to <paramref name="timeProvider"/>'s current time.</param>
    /// <param name="timeProvider">Clock consulted when <paramref name="now"/> is omitted; defaults to <see cref="TimeProvider.System"/>.</param>
    public static TimeSpan? Parse(
        IReadOnlyDictionary<string, string> headers,
        DateTimeOffset? now = null,
        TimeProvider? timeProvider = null)
    {
        if (TryGet(headers, Protocol.RetryAfterMsHeader) is { } rawMs
            && TryNumber(rawMs, out var ms)
            && ms >= 0)
        {
            return TimeSpan.FromMilliseconds(ms);
        }

        if (TryGet(headers, Protocol.RetryAfterHeader) is not { } raw) return null;
        if (TryNumber(raw, out var seconds))
        {
            return seconds >= 0 ? TimeSpan.FromSeconds(seconds) : null;
        }
        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out var date))
        {
            var delay = date - (now ?? (timeProvider ?? TimeProvider.System).GetUtcNow());
            return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
        }
        return null;
    }

    /// <summary>Header lookup that works for case-sensitive dictionaries too.</summary>
    private static string? TryGet(IReadOnlyDictionary<string, string> headers, string name)
    {
        if (headers.TryGetValue(name, out var direct)) return direct;
        foreach (var (key, value) in headers)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase)) return value;
        }
        return null;
    }

    private static bool TryNumber(string raw, out double value) =>
        double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
}
