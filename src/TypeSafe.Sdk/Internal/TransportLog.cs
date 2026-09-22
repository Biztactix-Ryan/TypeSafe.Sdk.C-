using Microsoft.Extensions.Logging;

namespace TypeSafe.Internal;

/// <summary>
/// Every log event the SDK emits, as source-generated <c>[LoggerMessage]</c> methods so that a
/// structured sink sees named properties and a stable <see cref="EventId"/> instead of parsed text.
/// The generator ships with <c>Microsoft.Extensions.Logging.Abstractions</c> and emits no reflection,
/// so these calls stay trim- and AOT-safe.
/// </summary>
/// <remarks>
/// <para>Event ids are part of the SDK's observable contract; they are never reused or renumbered.</para>
/// <list type="table">
///   <listheader><term>Id</term><term>Name</term><term>Level</term><term>Properties</term></listheader>
///   <item><term>1001</term><term>RequestSending</term><term>Debug</term>
///     <description>RequestNumber, Method, Path, Url, Attempt, Headers, Body</description></item>
///   <item><term>1002</term><term>ResponseReceived</term><term>Information</term>
///     <description>RequestNumber, Method, Path, StatusCode, ElapsedMs, Attempt</description></item>
///   <item><term>1003</term><term>ResponseReceivedWithRequestId</term><term>Information</term>
///     <description>RequestNumber, Method, Path, StatusCode, ElapsedMs, Attempt, RequestId</description></item>
///   <item><term>1004</term><term>ResponseBody</term><term>Debug</term>
///     <description>RequestNumber, Method, Path, Headers, Body</description></item>
///   <item><term>1005</term><term>RequestCancelled</term><term>Information</term>
///     <description>RequestNumber, Method, Path, ElapsedMs</description></item>
///   <item><term>1006</term><term>AttemptTimedOut</term><term>Information</term>
///     <description>RequestNumber, Method, Path, ElapsedMs, TimeoutMs</description></item>
///   <item><term>1007</term><term>ConnectionError</term><term>Information</term>
///     <description>RequestNumber, Method, Path, ElapsedMs, Error</description></item>
///   <item><term>1008</term><term>RetryBudgetExceeded</term><term>Information</term>
///     <description>RequestNumber, Method, Path, DelayMs, BudgetMs</description></item>
///   <item><term>1009</term><term>RetryScheduled</term><term>Information</term>
///     <description>RequestNumber, Method, Path, DelayMs, Retry, TotalRetries, Reason</description></item>
///   <item><term>1010</term><term>RetryCancelled</term><term>Information</term>
///     <description>RequestNumber, Method, Path, Attempt</description></item>
///   <item><term>1011</term><term>UnknownAnswerType</term><term>Warning</term>
///     <description>Question, Type</description></item>
/// </list>
/// <para>
/// <c>RequestNumber</c> counts calls made through one client, <c>Attempt</c> is 1-based within a
/// call, and <c>Path</c> is the request path while <c>Url</c> is the absolute target.
/// </para>
/// </remarks>
internal static partial class TransportLog
{
    /// <summary>An attempt is about to go on the wire, with credential headers already redacted.</summary>
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Debug,
        Message = "#{RequestNumber} {Method} {Path} -> {Url} attempt={Attempt} headers={Headers} body={Body}")]
    public static partial void RequestSending(
        ILogger logger, int requestNumber, string method, string path, string url, int attempt, string headers, string body);

    /// <summary>A response arrived, and the server named no request id.</summary>
    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "#{RequestNumber} {Method} {Path} <- {StatusCode} in {ElapsedMs}ms (attempt {Attempt})")]
    public static partial void ResponseReceived(
        ILogger logger, int requestNumber, string method, string path, int statusCode, long elapsedMs, int attempt);

    /// <summary>A response arrived carrying a request id. Same event as 1002, with the id attached.</summary>
    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "#{RequestNumber} {Method} {Path} <- {StatusCode} in {ElapsedMs}ms (attempt {Attempt}) (request {RequestId})")]
    public static partial void ResponseReceivedWithRequestId(
        ILogger logger, int requestNumber, string method, string path, int statusCode, long elapsedMs, int attempt, string requestId);

    /// <summary>The response headers and body, with credential headers redacted.</summary>
    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Debug,
        Message = "#{RequestNumber} {Method} {Path} <- headers={Headers} body={Body}")]
    public static partial void ResponseBody(
        ILogger logger, int requestNumber, string method, string path, string headers, string body);

    /// <summary>The caller's token cancelled an attempt in flight.</summary>
    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "#{RequestNumber} {Method} {Path} cancelled by caller after {ElapsedMs}ms")]
    public static partial void RequestCancelled(
        ILogger logger, int requestNumber, string method, string path, long elapsedMs);

    /// <summary>An attempt ran past the per-attempt timeout.</summary>
    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Information,
        Message = "#{RequestNumber} {Method} {Path} timed out after {ElapsedMs}ms (timeout {TimeoutMs}ms)")]
    public static partial void AttemptTimedOut(
        ILogger logger, int requestNumber, string method, string path, long elapsedMs, long timeoutMs);

    /// <summary>An attempt failed before any HTTP response was read.</summary>
    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Information,
        Message = "#{RequestNumber} {Method} {Path} connection error after {ElapsedMs}ms: {Error}")]
    public static partial void ConnectionError(
        ILogger logger, int requestNumber, string method, string path, long elapsedMs, string error);

    /// <summary>A retry was abandoned because its delay would have overrun the total retry budget.</summary>
    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Information,
        Message = "#{RequestNumber} {Method} {Path} not retrying: a {DelayMs}ms delay would exceed the {BudgetMs}ms retry budget")]
    public static partial void RetryBudgetExceeded(
        ILogger logger, int requestNumber, string method, string path, long delayMs, long budgetMs);

    /// <summary>A retry was scheduled after a retryable failure.</summary>
    [LoggerMessage(
        EventId = 1009,
        Level = LogLevel.Information,
        Message = "#{RequestNumber} {Method} {Path} retrying in {DelayMs}ms (retry {Retry}/{TotalRetries}) after {Reason}")]
    public static partial void RetryScheduled(
        ILogger logger, int requestNumber, string method, string path, long delayMs, int retry, int totalRetries, string reason);

    /// <summary>The caller's token cancelled the wait before a retry.</summary>
    [LoggerMessage(
        EventId = 1010,
        Level = LogLevel.Information,
        Message = "#{RequestNumber} {Method} {Path} cancelled by caller while waiting to retry (attempt {Attempt})")]
    public static partial void RetryCancelled(
        ILogger logger, int requestNumber, string method, string path, int attempt);

    /// <summary>An answer arrived with a type this SDK version does not model.</summary>
    [LoggerMessage(
        EventId = 1011,
        Level = LogLevel.Warning,
        Message = "Ignoring answer \"{Question}\" with unrecognized type \"{Type}\"")]
    public static partial void UnknownAnswerType(ILogger logger, string question, string type);
}
