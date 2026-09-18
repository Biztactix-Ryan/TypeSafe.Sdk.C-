using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using TypeSafe.Internal;

namespace TypeSafe;

/// <summary>State and named questions for <see cref="TypeSafeClient.SystemOneAsync(SystemOneRequest, RequestOptions?, CancellationToken)"/>.</summary>
public sealed class SystemOneRequest
{
    /// <summary>Create a request.</summary>
    /// <param name="state">
    /// Text, a JSON object, or an array to evaluate: a <see cref="string"/>, <see cref="JsonObject"/>, or
    /// <see cref="JsonArray"/> converts implicitly, and the default <see cref="Content"/> sends no state.
    /// Pass any other object through <see cref="Content.From(object?)"/>.
    /// </param>
    /// <param name="questions">Nonempty questions keyed by the names used to identify their answers.</param>
    public SystemOneRequest(Content state, IEnumerable<KeyValuePair<string, Question>> questions)
    {
        State = state;
        Questions = questions ?? throw new ArgumentNullException(nameof(questions));
    }

    /// <summary>Create a request from questions that carry their own names, as returned by <see cref="Question.Named"/>.</summary>
    /// <param name="state">
    /// Text, a JSON object, or an array to evaluate: a <see cref="string"/>, <see cref="JsonObject"/>, or
    /// <see cref="JsonArray"/> converts implicitly, and the default <see cref="Content"/> sends no state.
    /// Pass any other object through <see cref="Content.From(object?)"/>.
    /// </param>
    /// <param name="questions">
    /// Nonempty named questions. Each <see cref="INamedQuestion.Name"/> becomes the key its answer is read
    /// with, in the order given; the names must be distinct.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="questions"/> is <c>null</c>.</exception>
    /// <exception cref="TypeSafeException">A name is used more than once, or an entry is <c>null</c>.</exception>
    public SystemOneRequest(Content state, IEnumerable<INamedQuestion> questions)
        : this(state, Keyed(questions))
    {
    }

    /// <summary>Key named questions by their names, in order, rejecting a name that is used twice.</summary>
    private static List<KeyValuePair<string, Question>> Keyed(IEnumerable<INamedQuestion> questions)
    {
        ArgumentNullException.ThrowIfNull(questions);
        var keyed = new List<KeyValuePair<string, Question>>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var named in questions)
        {
            if (named is null) throw new TypeSafeException("A named question must not be null.");
            if (!names.Add(named.Name))
                throw new TypeSafeException($"Question name \"{named.Name}\" is used more than once.");
            keyed.Add(new KeyValuePair<string, Question>(named.Name, named.Question));
        }
        return keyed;
    }

    /// <summary>Text, a JSON object, or an array to evaluate. See <see href="https://docs.typesafe.ai/concepts/state">state</see>.</summary>
    public Content State { get; }

    /// <summary>Questions keyed by the names used to identify their answers.</summary>
    public IEnumerable<KeyValuePair<string, Question>> Questions { get; }

    /// <summary>Model override; <c>null</c> inherits the client's default model.</summary>
    public string? Model { get; init; }

    /// <summary>
    /// Additional top-level request-body fields, shallow-merged over the body after <c>state</c>, <c>model</c>,
    /// and <c>questions</c> are set. Merging is last-write-wins and object values are replaced, not deep-merged.
    /// </summary>
    public IEnumerable<KeyValuePair<string, JsonNode?>>? ExtraBody { get; init; }
}

/// <summary>The TypeSafe AI client surface, for dependency injection and test doubles.</summary>
public interface ITypeSafeClient : IDisposable
{
    /// <summary>The models available to the account.</summary>
    IModelsResource Models { get; }

    /// <summary>Answer named questions about text or structured state.</summary>
    Task<SystemOneResponse> SystemOneAsync(
        Content state,
        IEnumerable<KeyValuePair<string, Question>> questions,
        string? model = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Answer questions that carry their own names: <c>SystemOneAsync(state, [billing, tone])</c>.</summary>
    Task<SystemOneResponse> SystemOneAsync(Content state, params ReadOnlySpan<INamedQuestion> questions);

    /// <summary>Answer questions that carry their own names, with a model, options, or cancellation.</summary>
    Task<SystemOneResponse> SystemOneAsync(
        Content state,
        ReadOnlySpan<INamedQuestion> questions,
        string? model = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Answer named questions about text or structured state.</summary>
    Task<SystemOneResponse> SystemOneAsync(
        SystemOneRequest request,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Access to the models available to the account.</summary>
public interface IModelsResource
{
    /// <summary>List the models available to the account.</summary>
    Task<ListModelsResponse> ListAsync(RequestOptions? options = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Client for the <see href="https://typesafe.ai">TypeSafe AI</see> API.
/// </summary>
/// <example>
/// <code>
/// using var client = new TypeSafeClient();
/// var response = await client.SystemOneAsync(
///     state: Content.From(new { document = "I was charged twice. Please fix this ASAP." }),
///     questions: new Dictionary&lt;string, Question&gt;
///     {
///         ["category"] = Question.Choice("What is this ticket about?", "billing", "technical", "other"),
///     });
/// Console.WriteLine(response.Choices["category"].Choice);
/// </code>
/// </example>
public sealed class TypeSafeClient : ITypeSafeClient
{
    private readonly HttpClient _http;
    private readonly bool _disposeHttp;
    private readonly Transport _transport;
    private bool _disposed;

    /// <summary>Create a client configured from the environment: <c>TYPESAFE_API_KEY</c> and optional <c>TYPESAFE_*</c> overrides.</summary>
    public TypeSafeClient() : this(new TypeSafeClientOptions()) { }

    /// <summary>Create a client with an explicit API key and environment or default values for everything else.</summary>
    public TypeSafeClient(string apiKey) : this(new TypeSafeClientOptions { ApiKey = apiKey }) { }

    /// <summary>
    /// Create a client. Explicit options take precedence over environment variables, then SDK defaults.
    /// Empty or whitespace-only environment values are ignored.
    /// </summary>
    /// <exception cref="TypeSafeException">The API key is missing or configuration is invalid.</exception>
    /// <exception cref="ArgumentException">Both <see cref="TypeSafeClientOptions.HttpClient"/> and <see cref="TypeSafeClientOptions.HttpMessageHandler"/> are supplied.</exception>
    public TypeSafeClient(TypeSafeClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.HttpClient is not null && options.HttpMessageHandler is not null)
            throw new ArgumentException("HttpClient and HttpMessageHandler are mutually exclusive.", nameof(options));

        var apiKey = FromCodeOrEnv(options.ApiKey, TypeSafeConstants.ApiKeyEnv)
            ?? throw new TypeSafeException(
                $"No API key was provided. Pass ApiKey in TypeSafeClientOptions or set the {TypeSafeConstants.ApiKeyEnv} environment variable.");
        BaseUrl = (FromCodeOrEnv(options.BaseUrl, TypeSafeConstants.BaseUrlEnv) ?? TypeSafeConstants.DefaultBaseUrl).TrimEnd('/');
        DefaultModel = FromCodeOrEnv(options.DefaultModel, TypeSafeConstants.DefaultModelEnv) ?? TypeSafeConstants.DefaultModel;
        LogLevel = ResolveLogLevel(options.LogLevel);
        Logger = options.Logger ?? ConsoleLogger.Instance;
        Retry = (options.Retry ?? RetryPolicy.Default).Validate();
        DefaultHeaders = HeaderSnapshot.Copy(options.DefaultHeaders);

        if (options.HttpClient is not null)
        {
            _http = options.HttpClient;
            _disposeHttp = options.DisposeHttpClient;
            Timeout = ValidateTimeout(options.Timeout ?? _http.Timeout);
        }
        else
        {
            var handler = options.HttpMessageHandler ?? new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) };
            // The SDK applies its own per-attempt timeout, so the HttpClient's must not interfere.
            _http = new HttpClient(handler, disposeHandler: true) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
            _disposeHttp = true;
            Timeout = ValidateTimeout(options.Timeout ?? TypeSafeConstants.DefaultTimeout);
        }

        _transport = new Transport(_http, apiKey, BaseUrl, DefaultHeaders, Timeout, Retry, new SdkLog(Logger, LogLevel));
        Models = new ModelsResource(_transport);
    }

    /// <summary>API root with trailing slashes removed.</summary>
    public string BaseUrl { get; }

    /// <summary>Model used when a request omits <c>model</c>.</summary>
    public string DefaultModel { get; }

    /// <summary>Timeout per attempt.</summary>
    public TimeSpan Timeout { get; }

    /// <summary>Retry settings with constructor overrides applied. Derive per-call overrides with <c>client.Retry with { ... }</c>.</summary>
    public RetryPolicy Retry { get; }

    /// <summary>Additional headers sent with each request.</summary>
    public IReadOnlyDictionary<string, string> DefaultHeaders { get; }

    /// <summary>Configured minimum log level.</summary>
    public LogLevel LogLevel { get; }

    /// <summary>The configured logger.</summary>
    public ILogger Logger { get; }

    /// <summary>The models available to the account.</summary>
    public IModelsResource Models { get; }

    /// <summary>
    /// Answer named questions about text or structured state.
    /// See <see href="https://docs.typesafe.ai/concepts/system-one">System One</see> for details.
    /// </summary>
    /// <param name="state">
    /// Text, a JSON object, or an array to evaluate: a <see cref="string"/>, <see cref="JsonObject"/>, or
    /// <see cref="JsonArray"/> converts implicitly, and the default <see cref="Content"/> sends no state.
    /// Plain .NET objects go through <see cref="Content.From(object?)"/>, which serializes them with
    /// camelCase web defaults, or <see cref="Content.From{T}(T, System.Text.Json.Serialization.Metadata.JsonTypeInfo{T})"/>.
    /// </param>
    /// <param name="questions">Nonempty questions keyed by the names used to identify their answers.</param>
    /// <param name="model">Model override; <c>null</c> inherits <see cref="DefaultModel"/>.</param>
    /// <param name="options">Per-call timeout, retry, and header overrides.</param>
    /// <param name="cancellationToken">Cancels the request and any pending retries.</param>
    /// <returns>Answers keyed by question name, with model and token usage details.</returns>
    /// <exception cref="TypeSafeException">Questions are empty or a score question has no criteria.</exception>
    /// <exception cref="TypeSafeApiException">The server returns an unsuccessful HTTP response after any retries.</exception>
    /// <exception cref="TypeSafeApiConnectionException">The request cannot connect or times out after any retries.</exception>
    /// <exception cref="OperationCanceledException">The caller cancels the request.</exception>
    public Task<SystemOneResponse> SystemOneAsync(
        Content state,
        IEnumerable<KeyValuePair<string, Question>> questions,
        string? model = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return SystemOneAsync(new SystemOneRequest(state, questions) { Model = model }, options, cancellationToken);
    }

    /// <summary>
    /// Answer questions that carry their own names, as returned by the builders on <see cref="Question.Named"/>:
    /// <c>await client.SystemOneAsync(state, [billing, tone])</c>. The names key the answers exactly as the
    /// dictionary overload's keys do.
    /// </summary>
    /// <param name="state">
    /// Text, a JSON object, or an array to evaluate: a <see cref="string"/>, <see cref="JsonObject"/>, or
    /// <see cref="JsonArray"/> converts implicitly, and the default <see cref="Content"/> sends no state.
    /// </param>
    /// <param name="questions">Nonempty named questions with distinct names.</param>
    /// <returns>Answers keyed by question name, with model and token usage details.</returns>
    /// <exception cref="TypeSafeException">Questions are empty, a name repeats, or a score question has no criteria.</exception>
    /// <exception cref="TypeSafeApiException">The server returns an unsuccessful HTTP response after any retries.</exception>
    /// <exception cref="TypeSafeApiConnectionException">The request cannot connect or times out after any retries.</exception>
    public Task<SystemOneResponse> SystemOneAsync(Content state, params ReadOnlySpan<INamedQuestion> questions)
    {
        return SystemOneAsync(state, questions, model: null);
    }

    /// <summary>
    /// Answer questions that carry their own names, with a model, options, or cancellation. Not declared
    /// <c>params</c>: optional parameters cannot follow a parameter span, and a collection expression
    /// binds to the span either way — <c>await client.SystemOneAsync(state, [billing, tone], model: "...")</c>.
    /// </summary>
    /// <param name="state">
    /// Text, a JSON object, or an array to evaluate: a <see cref="string"/>, <see cref="JsonObject"/>, or
    /// <see cref="JsonArray"/> converts implicitly, and the default <see cref="Content"/> sends no state.
    /// </param>
    /// <param name="questions">Nonempty named questions with distinct names.</param>
    /// <param name="model">Model override; <c>null</c> inherits <see cref="DefaultModel"/>.</param>
    /// <param name="options">Per-call timeout, retry, and header overrides.</param>
    /// <param name="cancellationToken">Cancels the request and any pending retries.</param>
    /// <returns>Answers keyed by question name, with model and token usage details.</returns>
    /// <exception cref="TypeSafeException">Questions are empty, a name repeats, or a score question has no criteria.</exception>
    /// <exception cref="TypeSafeApiException">The server returns an unsuccessful HTTP response after any retries.</exception>
    /// <exception cref="TypeSafeApiConnectionException">The request cannot connect or times out after any retries.</exception>
    /// <exception cref="OperationCanceledException">The caller cancels the request.</exception>
    /// <remarks>
    /// The method is deliberately not <c>async</c>: an async method cannot take a byref-like parameter, so the
    /// span is copied before the request is handed to the shared overload.
    /// </remarks>
    public Task<SystemOneResponse> SystemOneAsync(
        Content state,
        ReadOnlySpan<INamedQuestion> questions,
        string? model = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = new SystemOneRequest(state, questions.ToArray()) { Model = model };
        return SystemOneAsync(request, options, cancellationToken);
    }

    /// <inheritdoc cref="SystemOneAsync(Content, IEnumerable{KeyValuePair{string, Question}}, string?, RequestOptions?, CancellationToken)"/>
    public Task<SystemOneResponse> SystemOneAsync(
        SystemOneRequest request,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfDisposed();

        var body = new JsonObject
        {
            ["state"] = request.State.Node?.DeepClone(),
            ["model"] = request.Model ?? DefaultModel,
            ["questions"] = Question.Normalize(request.Questions),
        };
        if (request.ExtraBody is not null)
        {
            foreach (var (key, value) in request.ExtraBody) body[key] = value?.DeepClone();
        }

        var log = _transport.Log;
        return _transport.SendAsync(
            HttpMethod.Post,
            Protocol.SystemOnePath,
            body,
            options,
            TypeSafeJsonContext.Default.SystemOneBody,
            decoded => SystemOneResponse.FromBody(decoded, log.Warn),
            cancellationToken);
    }

    /// <summary>Release network resources, disposing the underlying <see cref="HttpClient"/> when the SDK owns it.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_disposeHttp) _http.Dispose();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>Return the explicit value, falling back to a trimmed, nonblank environment value.</summary>
    private static string? FromCodeOrEnv(string? fromCode, string envVar)
    {
        if (fromCode is not null) return fromCode;
        var fromEnv = Environment.GetEnvironmentVariable(envVar)?.Trim();
        return string.IsNullOrEmpty(fromEnv) ? null : fromEnv;
    }

    private static LogLevel ResolveLogLevel(LogLevel? fromCode)
    {
        if (fromCode is { } level) return level;
        var fromEnv = Environment.GetEnvironmentVariable(TypeSafeConstants.LogLevelEnv)?.Trim();
        return string.IsNullOrEmpty(fromEnv) ? LogLevelParser.Default : LogLevelParser.Parse(fromEnv, TypeSafeConstants.LogLevelEnv);
    }

    /// <summary>Accept a positive timeout or <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>.</summary>
    internal static TimeSpan ValidateTimeout(TimeSpan timeout)
    {
        if (timeout == System.Threading.Timeout.InfiniteTimeSpan || timeout > TimeSpan.Zero) return timeout;
        throw new TypeSafeException($"`Timeout` must be a positive duration or Timeout.InfiniteTimeSpan, got {timeout}.");
    }
}
