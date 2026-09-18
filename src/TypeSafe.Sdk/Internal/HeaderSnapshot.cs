using System.Net.Http.Headers;

namespace TypeSafe.Internal;

/// <summary>Case-insensitive, immutable header dictionaries captured from HTTP messages.</summary>
internal static class HeaderSnapshot
{
    public static readonly IReadOnlyDictionary<string, string> Empty =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Copy headers into a case-insensitive dictionary; a <c>null</c> source yields an empty snapshot.</summary>
    public static IReadOnlyDictionary<string, string> Copy(IEnumerable<KeyValuePair<string, string>>? headers)
    {
        if (headers is null) return Empty;
        var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in headers) copy[name] = value;
        return copy;
    }

    /// <summary>Flatten response and content headers, joining repeated values with a comma.</summary>
    public static IReadOnlyDictionary<string, string> From(HttpResponseMessage response)
    {
        var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Add(copy, response.Headers);
        if (response.Content is not null) Add(copy, response.Content.Headers);
        return copy;
    }

    private static void Add(Dictionary<string, string> target, HttpHeaders headers)
    {
        foreach (var (name, values) in headers)
        {
            var joined = string.Join(", ", values);
            target[name] = target.TryGetValue(name, out var existing) ? $"{existing}, {joined}" : joined;
        }
    }

    public static string? RequestId(IReadOnlyDictionary<string, string> headers) =>
        headers.TryGetValue(TypeSafeConstants.RequestIdHeader, out var id) && id.Length > 0 ? id : null;
}
