using System.Net;
using System.Text.Json.Nodes;
using TypeSafe.Internal;

namespace TypeSafe;

/// <summary>Base exception for SDK failures.</summary>
public class TypeSafeException : Exception
{
    /// <summary>Create an SDK exception with a message.</summary>
    public TypeSafeException(string message) : base(message) { }

    /// <summary>Create an SDK exception with a message and an inner exception.</summary>
    public TypeSafeException(string message, Exception? innerException) : base(message, innerException) { }
}

/// <summary>An unsuccessful HTTP response from the API, with its body and request metadata.</summary>
public class TypeSafeApiException : TypeSafeException
{
    /// <summary>Describe an HTTP failure, deriving the message from the body unless one is supplied.</summary>
    /// <param name="status">HTTP response status code.</param>
    /// <param name="body">The parsed JSON body, a string node for plain text, or <c>null</c> for an empty body.</param>
    /// <param name="headers">HTTP response headers.</param>
    /// <param name="message">Optional message override; when omitted, a message is extracted from the body.</param>
    /// <param name="endpoint">The request method and URL, when available.</param>
    public TypeSafeApiException(
        int status,
        JsonNode? body,
        IReadOnlyDictionary<string, string>? headers = null,
        string? message = null,
        string? endpoint = null)
        : this(status, body, HeaderSnapshot.Copy(headers), message ?? Describe(body), endpoint, isDetail: true)
    {
    }

    private TypeSafeApiException(
        int status,
        JsonNode? body,
        IReadOnlyDictionary<string, string> headers,
        string detail,
        string? endpoint,
        bool isDetail)
        : base(Format(status, detail, endpoint, HeaderSnapshot.RequestId(headers)))
    {
        Status = status;
        Body = body;
        Headers = headers;
        Endpoint = endpoint;
        Detail = detail;
    }

    /// <summary>HTTP response status code.</summary>
    public int Status { get; }

    /// <summary>HTTP response status code as an enum.</summary>
    public HttpStatusCode StatusCode => (HttpStatusCode)Status;

    /// <summary>The server's JSON error body, a string node for plain text, or <c>null</c> for an empty body.</summary>
    public JsonNode? Body { get; }

    /// <summary>HTTP response headers, keyed case-insensitively.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>The request method and URL, without credentials, query parameters, or fragment, when available.</summary>
    public string? Endpoint { get; }

    /// <summary>The message extracted from the body (or supplied), without the status, endpoint, or request ID.</summary>
    public string Detail { get; }

    /// <summary>The <c>x-typesafe-request-id</c> response header, or <c>null</c> if absent.</summary>
    public string? RequestId => HeaderSnapshot.RequestId(Headers);

    /// <summary>Create the exception subclass matching an HTTP status code.</summary>
    public static TypeSafeApiException FromResponse(
        int status,
        JsonNode? body,
        IReadOnlyDictionary<string, string>? headers = null,
        string? endpoint = null)
    {
        return status switch
        {
            400 => new TypeSafeBadRequestException(status, body, headers, endpoint: endpoint),
            401 => new TypeSafeAuthenticationException(status, body, headers, endpoint: endpoint),
            403 => new TypeSafePermissionDeniedException(status, body, headers, endpoint: endpoint),
            404 => new TypeSafeNotFoundException(status, body, headers, endpoint: endpoint),
            422 => new TypeSafeUnprocessableEntityException(status, body, headers, endpoint: endpoint),
            429 => new TypeSafeRateLimitException(status, body, headers, endpoint: endpoint),
            >= 500 => new TypeSafeInternalServerException(status, body, headers, endpoint: endpoint),
            _ => new TypeSafeApiException(status, body, headers, endpoint: endpoint),
        };
    }

    private static string Format(int status, string detail, string? endpoint, string? requestId)
    {
        var message = detail.Length > 0 ? $"{status} {detail}" : status.ToString();
        if (endpoint is not null) message = $"{endpoint}: {message}";
        if (requestId is not null) message += $" (request_id={requestId})";
        return message;
    }

    /// <summary>Extract a message from the body, falling back to a truncated raw body.</summary>
    private static string Describe(JsonNode? body)
    {
        var detail = ExtractMessage(body);
        if (!string.IsNullOrEmpty(detail)) return detail;
        if (body is null) return "status code (no body)";
        var raw = JsonContent.AsString(body) ?? body.ToJsonString();
        return raw.Length > Protocol.MaxErrorBodyLength
            ? raw[..Protocol.MaxErrorBodyLength] + "…"
            : raw;
    }

    /// <summary>Extract a message from a text, error, or validation response body.</summary>
    internal static string? ExtractMessage(JsonNode? body)
    {
        if (body is JsonValue)
        {
            var text = JsonContent.AsString(body);
            return string.IsNullOrEmpty(text) ? null : text;
        }
        if (body is not JsonObject obj) return null;

        var error = obj["error"];
        var message = obj["message"];
        var detail = obj["detail"];

        if (JsonContent.AsString(error) is { } errorText) return errorText;
        if (error is JsonObject errorObj && JsonContent.AsString(errorObj["message"]) is { } errorMessage) return errorMessage;
        if (JsonContent.AsString(message) is { } messageText) return messageText;
        if (JsonContent.AsString(detail) is { } detailText) return detailText;
        if (detail is JsonObject detailObj && JsonContent.AsString(detailObj["message"]) is { } detailMessage) return detailMessage;
        if (detail is JsonArray detailList) return DescribeValidationErrors(detailList);
        return null;
    }

    /// <summary>Format validation errors as semicolon-separated <c>path: message</c> entries.</summary>
    private static string? DescribeValidationErrors(JsonArray errors)
    {
        var parts = new List<string>();
        foreach (var entry in errors)
        {
            if (entry is not JsonObject item || JsonContent.AsString(item["msg"]) is not { } msg) continue;
            var path = "";
            if (item["loc"] is JsonArray loc)
            {
                var segments = loc
                    .Where(segment => segment is not null && JsonContent.AsString(segment) != "body")
                    .Select(segment => JsonContent.AsString(segment) ?? segment!.ToJsonString());
                path = string.Join(".", segments);
            }
            parts.Add(path.Length > 0 ? $"{path}: {msg}" : msg);
        }
        return parts.Count > 0 ? string.Join("; ", parts) : null;
    }
}

/// <summary>HTTP 400: the request was invalid.</summary>
public class TypeSafeBadRequestException : TypeSafeApiException
{
    /// <inheritdoc cref="TypeSafeApiException(int, JsonNode?, IReadOnlyDictionary{string, string}?, string?, string?)"/>
    public TypeSafeBadRequestException(int status, JsonNode? body, IReadOnlyDictionary<string, string>? headers = null, string? message = null, string? endpoint = null)
        : base(status, body, headers, message, endpoint) { }
}

/// <summary>HTTP 401: authentication failed.</summary>
public class TypeSafeAuthenticationException : TypeSafeApiException
{
    /// <inheritdoc cref="TypeSafeApiException(int, JsonNode?, IReadOnlyDictionary{string, string}?, string?, string?)"/>
    public TypeSafeAuthenticationException(int status, JsonNode? body, IReadOnlyDictionary<string, string>? headers = null, string? message = null, string? endpoint = null)
        : base(status, body, headers, message, endpoint) { }
}

/// <summary>HTTP 403: access was denied.</summary>
public class TypeSafePermissionDeniedException : TypeSafeApiException
{
    /// <inheritdoc cref="TypeSafeApiException(int, JsonNode?, IReadOnlyDictionary{string, string}?, string?, string?)"/>
    public TypeSafePermissionDeniedException(int status, JsonNode? body, IReadOnlyDictionary<string, string>? headers = null, string? message = null, string? endpoint = null)
        : base(status, body, headers, message, endpoint) { }
}

/// <summary>HTTP 404: the resource was not found.</summary>
public class TypeSafeNotFoundException : TypeSafeApiException
{
    /// <inheritdoc cref="TypeSafeApiException(int, JsonNode?, IReadOnlyDictionary{string, string}?, string?, string?)"/>
    public TypeSafeNotFoundException(int status, JsonNode? body, IReadOnlyDictionary<string, string>? headers = null, string? message = null, string? endpoint = null)
        : base(status, body, headers, message, endpoint) { }
}

/// <summary>HTTP 422: the request failed server validation.</summary>
public class TypeSafeUnprocessableEntityException : TypeSafeApiException
{
    /// <inheritdoc cref="TypeSafeApiException(int, JsonNode?, IReadOnlyDictionary{string, string}?, string?, string?)"/>
    public TypeSafeUnprocessableEntityException(int status, JsonNode? body, IReadOnlyDictionary<string, string>? headers = null, string? message = null, string? endpoint = null)
        : base(status, body, headers, message, endpoint) { }
}

/// <summary>HTTP 429: the rate limit was exceeded.</summary>
public class TypeSafeRateLimitException : TypeSafeApiException
{
    /// <inheritdoc cref="TypeSafeApiException(int, JsonNode?, IReadOnlyDictionary{string, string}?, string?, string?)"/>
    public TypeSafeRateLimitException(int status, JsonNode? body, IReadOnlyDictionary<string, string>? headers = null, string? message = null, string? endpoint = null)
        : base(status, body, headers, message, endpoint)
    {
        RetryAfter = RetryAfterParser.Parse(Headers);
    }

    /// <summary>The server's requested wait from <c>retry-after-ms</c> or <c>Retry-After</c>, or <c>null</c> when absent or invalid.</summary>
    public TimeSpan? RetryAfter { get; }
}

/// <summary>HTTP 5xx: the server failed to process the request.</summary>
public class TypeSafeInternalServerException : TypeSafeApiException
{
    /// <inheritdoc cref="TypeSafeApiException(int, JsonNode?, IReadOnlyDictionary{string, string}?, string?, string?)"/>
    public TypeSafeInternalServerException(int status, JsonNode? body, IReadOnlyDictionary<string, string>? headers = null, string? message = null, string? endpoint = null)
        : base(status, body, headers, message, endpoint) { }
}

/// <summary>A successful HTTP response whose body was missing or structurally invalid required data.</summary>
public class TypeSafeApiResponseValidationException : TypeSafeApiException
{
    /// <summary>Describe an unparseable response, naming the first missing or structurally invalid field.</summary>
    public TypeSafeApiResponseValidationException(int status, JsonNode? body, IReadOnlyDictionary<string, string>? headers, string fieldPath, string? endpoint = null)
        : base(status, body, headers, $"Invalid response data at '{fieldPath}'.", endpoint)
    {
        FieldPath = fieldPath;
    }

    /// <summary>Dotted path to the offending field, such as <c>answers.tone.confidence</c>.</summary>
    public string FieldPath { get; }
}

/// <summary>A request failed without an HTTP response (DNS, TLS, connection closed, interrupted body, etc.).</summary>
public class TypeSafeApiConnectionException : TypeSafeException
{
    /// <summary>Create a connection exception.</summary>
    public TypeSafeApiConnectionException(string message = "Connection error.", Exception? innerException = null)
        : base(message, innerException) { }
}

/// <summary>The full response did not arrive within the per-attempt timeout. A kind of <see cref="TypeSafeApiConnectionException"/>.</summary>
public class TypeSafeApiTimeoutException : TypeSafeApiConnectionException
{
    /// <summary>Create a timeout exception for the configured timeout.</summary>
    public TypeSafeApiTimeoutException(TimeSpan timeout, Exception? innerException = null)
        : base($"Request timed out after {timeout.TotalMilliseconds:0}ms.", innerException)
    {
        Timeout = timeout;
    }

    /// <summary>The per-attempt timeout that was exceeded.</summary>
    public TimeSpan Timeout { get; }
}
