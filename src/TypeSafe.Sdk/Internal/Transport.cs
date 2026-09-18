using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

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
    public async Task<TResponse> SendAsync<TResponse>(
        HttpMethod method,
        string path,
        JsonNode? body,
        RequestOptions? options,
        Func<JsonNode?, TResponse> parse,
        CancellationToken cancellationToken)
        where TResponse : ApiResponse
    {
        cancellationToken.ThrowIfCancellationRequested();
        var url = _baseUrl + path;
        var endpoint = $"{method} {url}";
        var timeout = options?.Timeout is { } perCall ? TypeSafeClient.ValidateTimeout(perCall) : _timeout;
        var policy = options?.Retry is { } perCallPolicy ? perCallPolicy.Validate() : _retry;
        var headers = BuildHeaders(options?.Headers, body is not null);
        var content = body is null ? null : JsonContent.Serialize(body);
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
            var status = (int)response.StatusCode;
            _log.Info($"{tag} <- {status} in {Elapsed(attemptStarted)}{(requestId is null ? "" : $" (request {requestId})")}");

            string? text;
            try
            {
                text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                response.Dispose();
                var connectionError = new TypeSafeApiConnectionException($"Connection error: {error.Message}", error);
                if (retriesLeft <= 0 || !policy.ShouldRetry(connectionError)) throw connectionError;
                await BackOffAsync(tag, attempt, retriesLeft, connectionError.Message, null, policy, started, connectionError, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var parsedBody = JsonContent.ParseLenient(text);
            if (_log.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
                _log.Debug($"{tag} <- headers={Redaction.Describe(responseHeaders)} body={text}");

            TypeSafeApiException failure;
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var result = parse(parsedBody);
                    result.Attach(response, responseHeaders);
                    return result;
                }
                catch (ResponseFieldException error)
                {
                    failure = new TypeSafeApiResponseValidationException(status, parsedBody, responseHeaders, error.FieldPath, endpoint);
                }
            }
            else
            {
                failure = TypeSafeApiException.FromResponse(status, parsedBody, responseHeaders, endpoint);
            }

            response.Dispose();
            if (retriesLeft <= 0 || !policy.ShouldRetry(failure)) throw failure;
            await BackOffAsync(tag, attempt, retriesLeft, status.ToString(), responseHeaders, policy, started, failure, cancellationToken).ConfigureAwait(false);
        }
    }

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
            throw new TypeSafeApiConnectionException($"Connection error: {error.Message}", error);
        }
    }

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
