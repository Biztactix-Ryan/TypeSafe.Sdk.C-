using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace TypeSafe.Internal;

/// <summary>Shared HTTP request preparation, retry loop, logging, and dispatch to response parsers.</summary>
internal sealed class Transport
{

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly IReadOnlyDictionary<string, string> _defaultHeaders;
    private readonly TimeSpan _timeout;
    private readonly RetryPolicy _retry;
    private readonly SdkLog _log;
    private int _requestCount;

    public Transport(
        HttpClient http,
        string apiKey,
        string baseUrl,
        IReadOnlyDictionary<string, string> defaultHeaders,
        TimeSpan timeout,
        RetryPolicy retry,
        SdkLog log)
    {
        _http = http;
        _apiKey = apiKey;
        _baseUrl = baseUrl;
        _defaultHeaders = defaultHeaders;
        _timeout = timeout;
        _retry = retry;
        _log = log;
    }

    public SdkLog Log => _log;

    /// <summary>Send a JSON request with retries and decode the successful response.</summary>
    /// <param name="method">HTTP method.</param>
    /// <param name="path">Path appended to the base URL.</param>
    /// <param name="body">
    /// Request body, or <c>null</c> for a bodyless request. It is written verbatim through the
    /// source-generated <see cref="JsonObject"/> metadata, so the caller's key order is the wire order.
    /// </param>
    /// <param name="options">Per-call overrides.</param>
    /// <param name="bodyType">Source-generated metadata for the success body, which is read with no reflection.</param>
    /// <param name="project">Projects a decoded body onto the public response type.</param>
    /// <param name="cancellationToken">Cancels the request and any pending retry.</param>
    public async Task<TResponse> SendAsync<TBody, TResponse>(
        HttpMethod method,
        string path,
        JsonObject? body,
        RequestOptions? options,
        JsonTypeInfo<TBody> bodyType,
        Func<TBody, TResponse> project,
        CancellationToken cancellationToken)
        where TBody : class
        where TResponse : ApiResponse
    {
        cancellationToken.ThrowIfCancellationRequested();
        var url = _baseUrl + path;
        var endpoint = $"{method} {url}";
        var timeout = options?.Timeout is { } perCall ? TypeSafeClient.ValidateTimeout(perCall) : _timeout;
        var policy = options?.Retry is { } perCallPolicy ? perCallPolicy.Validate() : _retry;
        var headers = BuildHeaders(options?.Headers, body is not null);
        var content = body is null ? null : JsonSerializer.Serialize(body, TypeSafeJsonContext.Default.JsonObject);
        // Numbered so concurrent requests, and the attempts within one, can be told apart in the logs.
        var tag = $"#{Interlocked.Increment(ref _requestCount)} {method} {path}";
        var started = Stopwatch.GetTimestamp();

        for (var attempt = 0; ; attempt++)
        {
            var retriesLeft = policy.MaxRetries - attempt;
            var attemptHeaders = attempt == 0
                ? headers
                : new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase)
                {
                    [Protocol.RetryCountHeader] = attempt.ToString(),
                };
            if (_log.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
                _log.Debug($"{tag} -> {url} headers={Redaction.Describe(attemptHeaders)} body={content ?? "(none)"}");

            var attemptStarted = Stopwatch.GetTimestamp();
            HttpResponseMessage response;
            try
            {
                response = await AttemptAsync(tag, method, url, attemptHeaders, content, timeout, cancellationToken).ConfigureAwait(false);
            }
            catch (TypeSafeApiConnectionException error)
            {
                if (retriesLeft <= 0 || !policy.ShouldRetry(error)) throw;
                await BackOffAsync(tag, attempt, retriesLeft, error.Message, null, policy, started, error, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var responseHeaders = HeaderSnapshot.From(response);
            var requestId = HeaderSnapshot.RequestId(responseHeaders);
            var statusCode = response.StatusCode;
            var status = (int)statusCode;
            _log.Info($"{tag} <- {status} in {Elapsed(attemptStarted)}{(requestId is null ? "" : $" (request {requestId})")}");

            string? text;
            try
            {
                text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                response.Dispose();
                var connectionError = new TypeSafeApiConnectionException($"Connection error: {error.Message}", error, TransportErrorOf(error));
                if (retriesLeft <= 0 || !policy.ShouldRetry(connectionError)) throw connectionError;
                await BackOffAsync(tag, attempt, retriesLeft, connectionError.Message, null, policy, started, connectionError, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (_log.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
                _log.Debug($"{tag} <- headers={Redaction.Describe(responseHeaders)} body={text}");

            TypeSafeApiException failure;
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var result = project(Decode(text, bodyType));
                    result.Attach(response, responseHeaders);
                    return result;
                }
                catch (JsonException error)
                {
                    // The body is still reported the way an error body is: leniently decoded, or a
                    // string node when it was not JSON at all.
                    failure = new TypeSafeApiResponseValidationException(
                        statusCode, JsonContent.ParseLenient(text), responseHeaders, FieldPath(error.Path), endpoint);
                }
            }
            else
            {
                failure = TypeSafeApiException.FromResponse(statusCode, JsonContent.ParseLenient(text), responseHeaders, endpoint);
            }

            response.Dispose();
            if (retriesLeft <= 0 || !policy.ShouldRetry(failure)) throw failure;
            await BackOffAsync(tag, attempt, retriesLeft, status.ToString(), responseHeaders, policy, started, failure, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Deserialize a success body through the source-generated context. An absent, empty or literal
    /// <c>null</c> body is as unusable as a malformed one, so it is reported the same way.
    /// </summary>
    private static TBody Decode<TBody>(string? text, JsonTypeInfo<TBody> bodyType) where TBody : class
    {
        var decoded = string.IsNullOrEmpty(text) ? null : JsonSerializer.Deserialize(text, bodyType);
        return decoded ?? throw new JsonException("The response body was empty.");
    }

    /// <summary>
    /// Translate a <see cref="JsonException.Path"/> (<c>$.answers.tone.confidence</c>) into an SDK
    /// field path (<c>answers.tone.confidence</c>); the root, and an exception with no path at all,
    /// become the empty string.
    /// </summary>
    private static string FieldPath(string? jsonPath) => jsonPath switch
    {
        null or "" or "$" => "",
        var path when path.StartsWith("$.", StringComparison.Ordinal) => path[2..],
        var path when path.StartsWith('$') => path[1..],
        var path => path,
    };

    /// <summary>Merge default and per-call headers case-insensitively (last wins), then apply the protected SDK headers.</summary>
    private Dictionary<string, string> BuildHeaders(IEnumerable<KeyValuePair<string, string>>? perCall, bool hasBody)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in _defaultHeaders) merged[name] = value;
        if (perCall is not null)
        {
            foreach (var (name, value) in perCall) merged[name] = value;
        }
        // User-supplied values can't clobber auth, SDK identification, or the JSON content type.
        foreach (var name in Protocol.ProtectedHeaders) merged.Remove(name);
        merged[Protocol.AuthorizationHeader] = $"Bearer {_apiKey}";
        merged[Protocol.AcceptHeader] = Protocol.JsonContentType;
        merged[Protocol.UserAgentHeader] = $"{Protocol.SdkName}/{TypeSafeConstants.Version}";
        merged[Protocol.SdkHeader] = $"{Protocol.SdkName}/{TypeSafeConstants.Version}";
        merged[Protocol.RuntimeHeader] = RuntimeInfo.Description;
        if (hasBody) merged[Protocol.ContentTypeHeader] = Protocol.JsonContentType;
        return merged;
    }

    /// <summary>
    /// One HTTP round trip, including body delivery, under the per-attempt timeout. The caller's token
    /// and the timeout share one cancellation; the caller's token is checked first to choose the error.
    /// </summary>
    private async Task<HttpResponseMessage> AttemptAsync(
        string tag,
        HttpMethod method,
        string url,
        IReadOnlyDictionary<string, string> headers,
        string? content,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url);
        if (content is not null)
        {
            request.Content = new StringContent(content, Encoding.UTF8, new MediaTypeHeaderValue(Protocol.JsonContentType));
        }
        foreach (var (name, value) in headers)
        {
            if (name.Equals(Protocol.ContentTypeHeader, StringComparison.OrdinalIgnoreCase)) continue;
            if (!request.Headers.TryAddWithoutValidation(name, value))
            {
                request.Content?.Headers.TryAddWithoutValidation(name, value);
            }
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout != System.Threading.Timeout.InfiniteTimeSpan) timeoutSource.CancelAfter(timeout);

        var started = Stopwatch.GetTimestamp();
        try
        {
            // ResponseContentRead buffers the whole body under the timeout, so interrupted bodies surface here.
            return await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException error) when (cancellationToken.IsCancellationRequested)
        {
            _log.Info($"{tag} cancelled by caller after {Elapsed(started)}");
            throw new OperationCanceledException("The request was cancelled.", error, cancellationToken);
        }
        catch (OperationCanceledException error)
        {
            _log.Info($"{tag} timed out after {Elapsed(started)}");
            throw new TypeSafeApiTimeoutException(timeout, error);
        }
        catch (Exception error) when (error is HttpRequestException or IOException)
        {
            _log.Info($"{tag} connection error after {Elapsed(started)}: {error.Message}");
            throw new TypeSafeApiConnectionException($"Connection error: {error.Message}", error, TransportErrorOf(error));
        }
    }

    /// <summary>The transport failure an exception names, or <c>null</c> when it is not an <see cref="HttpRequestException"/>.</summary>
    private static HttpRequestError? TransportErrorOf(Exception error) => (error as HttpRequestException)?.HttpRequestError;

    /// <summary>Wait before retrying; throws the last error when the total budget would be exceeded and rethrows caller cancellation.</summary>
    private async Task BackOffAsync(
        string tag,
        int attempt,
        int retriesLeft,
        string reason,
        IReadOnlyDictionary<string, string>? headers,
        RetryPolicy policy,
        long started,
        Exception lastError,
        CancellationToken cancellationToken)
    {
        var delay = policy.DelayFor(attempt, headers);
        if (policy.TotalTimeout is { } budget && Stopwatch.GetElapsedTime(started) + delay >= budget)
        {
            _log.Info($"{tag} not retrying: a {delay.TotalMilliseconds:0}ms delay would exceed the {budget.TotalMilliseconds:0}ms retry budget");
            ExceptionDispatchInfo.Capture(lastError).Throw();
        }
        var nth = attempt + 1;
        var total = attempt + retriesLeft;
        _log.Info($"{tag} retrying in {delay.TotalMilliseconds:0}ms (retry {nth}/{total}) after {reason}");
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException error)
        {
            _log.Info($"{tag} cancelled by caller while waiting to retry");
            throw new OperationCanceledException("The request was cancelled while waiting to retry.", error, cancellationToken);
        }
    }

    private static string Elapsed(long startedTimestamp) =>
        $"{Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds:0}ms";
}
