using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TypeSafe.Internal;

namespace TypeSafe;

/// <summary>A response object that also exposes the originating HTTP response and request ID.</summary>
public abstract class ApiResponse
{
    /// <summary>The <c>x-typesafe-request-id</c> response header, or <c>null</c> when the response did not include one.</summary>
    public string? RequestId { get; private set; }

    /// <summary>HTTP response status code, or <c>0</c> when not created from an HTTP response.</summary>
    public HttpStatusCode StatusCode { get; private set; }

    /// <summary>HTTP response headers, keyed case-insensitively.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; private set; } = HeaderSnapshot.Empty;

    /// <summary>The underlying HTTP response with its buffered body, or <c>null</c> when not created from an HTTP response.</summary>
    public HttpResponseMessage? RawHttpResponse { get; private set; }

    internal void Attach(HttpResponseMessage response, IReadOnlyDictionary<string, string> headers)
    {
        RawHttpResponse = response;
        StatusCode = response.StatusCode;
        Headers = headers;
        RequestId = HeaderSnapshot.RequestId(headers);
    }

    /// <summary>
    /// Report a field of this response the SDK could not make sense of, exactly as the transport reports
    /// a body it could not decode: the same status code, headers and request ID, the endpoint recovered
    /// from the originating request, and the body parsed leniently (a string node when it was not JSON).
    /// </summary>
    /// <param name="fieldPath">Dotted path to the offending field, such as <c>answers.tone.choice</c>.</param>
    internal TypeSafeApiResponseValidationException Invalid(string fieldPath) =>
        new(StatusCode, ReadBody(), Headers, fieldPath, ReadEndpoint());

    /// <summary>
    /// The response body as a <see cref="JsonNode"/>, or <c>null</c> when this response was not created
    /// from an HTTP response or its content can no longer be read (streamed away, or disposed).
    /// </summary>
    private JsonNode? ReadBody()
    {
        if (RawHttpResponse?.Content is not { } content) return null;
        try
        {
            var stream = content.ReadAsStream();
            if (stream.CanSeek) stream.Position = 0;
            // The stream is left open: it belongs to RawHttpResponse, which the caller still owns.
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            return JsonContent.ParseLenient(reader.ReadToEnd());
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or NotSupportedException or InvalidOperationException or HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>The method and URL of the request this response answered, or <c>null</c> when unknown.</summary>
    private string? ReadEndpoint() =>
        RawHttpResponse?.RequestMessage is { RequestUri: { } url } request ? $"{request.Method} {url}" : null;
}

/// <summary>
/// An answer to a single question, identified by its <see cref="Type"/>. The record is not abstract
/// so that an answer whose shape this SDK version does not model can still be carried as a bare
/// <see cref="Answer"/>, with its fields in <see cref="AdditionalProperties"/>.
/// </summary>
[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(NoulAnswer), "noul")]
[JsonDerivedType(typeof(ChoiceAnswer), "choice")]
[JsonDerivedType(typeof(ScoreAnswer), "score")]
public record Answer
{
    /// <summary>
    /// The wire discriminator: <c>noul</c>, <c>choice</c>, or <c>score</c>, or the empty string on a
    /// bare <see cref="Answer"/>, which models no primitive of its own. Always ignored by the
    /// serializer: <c>type</c> is polymorphic metadata, written and read by the discriminator.
    /// </summary>
    [JsonIgnore]
    public virtual string Type => "";

    /// <summary>
    /// Fields the server sent that this answer record does not model, so a newer server never
    /// breaks an older client. <c>null</c> when the answer was built in code rather than decoded.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Settable rather than init-only because the <c>System.Text.Json</c> source generator cannot
    /// assign an init-only property: it models one as a constructor parameter, and extension data
    /// may not bind to a constructor parameter.
    /// </para>
    /// <para>
    /// On a bare <see cref="Answer"/> — an answer whose <c>type</c> this SDK version does not model —
    /// this bag holds the whole answer object, the <c>type</c> discriminator included, so
    /// <c>AdditionalProperties["type"]</c> names the unrecognized type. That is the work of
    /// <see cref="AnswerMapConverter"/>, not of
    /// <see cref="JsonUnknownDerivedTypeHandling.FallBackToBaseType"/>, which applies to writing only:
    /// reading such an answer straight through <c>TypeSafeJsonContext.Default.Answer</c> throws
    /// <c>JsonException: Read unrecognized type discriminator id 'rank'.</c> (verified in
    /// <c>JsonContextTests</c>). Since the discriminator is carried as ordinary extension data,
    /// writing a bare answer back out reproduces the payload it arrived as.
    /// </para>
    /// </remarks>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>A yes/no answer. See the <see href="https://docs.typesafe.ai/primitives/noul">noul primitive</see>.</summary>
/// <param name="Noul">Probability of a yes answer, from zero to one.</param>
public sealed record NoulAnswer(
    [property: JsonPropertyName("noul")] double Noul) : Answer
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override string Type => "noul";

    /// <summary>
    /// Alias of <see cref="Noul"/> for callers who prefer the .NET-style name.
    /// Computed, never sent or received on the wire.
    /// </summary>
    [JsonIgnore]
    public double Probability => Noul;
}

/// <summary>A selected label and its probabilities. See the <see href="https://docs.typesafe.ai/primitives/choice">choice primitive</see>.</summary>
/// <param name="Choice">The selected label.</param>
/// <param name="Confidence">Reported confidence in the selected label.</param>
/// <param name="Probabilities">Probabilities keyed by label.</param>
public sealed record ChoiceAnswer(
    [property: JsonPropertyName("choice")] string Choice,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("probabilities")] IReadOnlyDictionary<string, double> Probabilities) : Answer
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override string Type => "choice";

    /// <summary>
    /// Alias of <see cref="Choice"/> for callers who prefer the .NET-style name.
    /// Computed, never sent or received on the wire.
    /// </summary>
    [JsonIgnore]
    public string Label => Choice;
}

/// <summary>
/// A selected enum member and its probabilities: the answer to a <see cref="ChoiceQuestion{TEnum}"/>, so
/// the choice is a <typeparamref name="TEnum"/> rather than a label the caller has to re-spell.
/// </summary>
/// <typeparam name="TEnum">The enum whose members are the available labels.</typeparam>
/// <param name="Choice">The selected member.</param>
/// <param name="Confidence">Reported confidence in the selected member.</param>
/// <param name="Probabilities">Probabilities keyed by member.</param>
/// <remarks>
/// Built on the response side from the wire <see cref="ChoiceAnswer"/> when the answer is read through a
/// <see cref="Named{TAnswer}"/> of this type, so it is deliberately not registered with the
/// source-generated context: nothing ever serializes or deserializes it. Every label the server sent must
/// be one <typeparamref name="TEnum"/> declares; one that is not is reported as invalid response data.
/// </remarks>
public sealed record ChoiceAnswer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(
    TEnum Choice,
    double Confidence,
    IReadOnlyDictionary<TEnum, double> Probabilities) : Answer
    where TEnum : struct, Enum
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override string Type => "choice";
}

/// <summary>An expected score with its rubric and probabilities. See the <see href="https://docs.typesafe.ai/primitives/score">score primitive</see>.</summary>
/// <param name="Score">Expected score, which may fall between the integer rubric levels.</param>
/// <param name="Confidence">Reported confidence in the score.</param>
/// <param name="Legend">Rubric descriptions keyed by integer score, as text, a JSON object, or an array.</param>
/// <param name="Probabilities">Probabilities keyed by integer score.</param>
public sealed record ScoreAnswer(
    [property: JsonPropertyName("score")] double Score,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("legend")] IReadOnlyDictionary<int, JsonNode?> Legend,
    [property: JsonPropertyName("probabilities")] IReadOnlyDictionary<int, double> Probabilities) : Answer
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override string Type => "score";
}

/// <summary>
/// An expected score with its rubric and probabilities keyed by <typeparamref name="TEnum"/>: the answer
/// to a <see cref="ScoreQuestion{TEnum}"/>, so every rubric level is a named member rather than an integer
/// the caller has to interpret, and <see cref="Nearest"/> names the level the score lands on.
/// </summary>
/// <typeparam name="TEnum">The enum whose members are the rubric levels, valued <c>0</c> to <c>N-1</c>.</typeparam>
/// <param name="Score">Expected score, which may fall between the integer rubric levels.</param>
/// <param name="Confidence">Reported confidence in the score.</param>
/// <param name="Legend">Rubric descriptions keyed by level, as text, a JSON object, or an array.</param>
/// <param name="Probabilities">Probabilities keyed by level.</param>
/// <remarks>
/// Built on the response side from the wire <see cref="ScoreAnswer"/> when the answer is read through a
/// <see cref="Named{TAnswer}"/> of this type, so it is deliberately not registered with the
/// source-generated context: nothing ever serializes or deserializes it. Every score the server keyed the
/// legend or the probabilities by must be a level <typeparamref name="TEnum"/> declares; one that is not
/// is reported as invalid response data.
/// </remarks>
public sealed record ScoreAnswer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(
    double Score,
    double Confidence,
    IReadOnlyDictionary<TEnum, JsonNode?> Legend,
    IReadOnlyDictionary<TEnum, double> Probabilities) : Answer
    where TEnum : struct, Enum
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override string Type => "score";

    /// <summary>
    /// The rubric level <see cref="Score"/> lands on: the score rounded half away from zero — <c>1.4</c> is
    /// level <c>1</c> and <c>1.5</c> is level <c>2</c> — and clamped to the rubric, so a score above the
    /// top level is the top level and a negative one is the bottom. Computed, never sent or received on
    /// the wire.
    /// </summary>
    [JsonIgnore]
    public TEnum Nearest => Rubric.Nearest<TEnum>(Score);
}

/// <summary>Token counts for a request, when reported by the API.</summary>
/// <param name="InputTokens">Number of input tokens used, or <c>null</c> when the API did not report it.</param>
/// <param name="OutputTokens">Number of output tokens used, or <c>null</c> when the API did not report it.</param>
public sealed record Usage(
    [property: JsonPropertyName("input_tokens")] long? InputTokens = null,
    [property: JsonPropertyName("output_tokens")] long? OutputTokens = null);

/// <summary>
/// The wire shape of a <c>POST /v1/systemone</c> response body. Decoded by the source-generated
/// <c>TypeSafeJsonContext</c>; <see cref="SystemOneResponse"/> is the public projection of it.
/// </summary>
/// <param name="Model">The model that answered the request.</param>
/// <param name="Usage">Token usage for the request.</param>
/// <param name="Answers">
/// Answers keyed by question name, read through <see cref="AnswerMapConverter"/> so an answer type
/// this SDK version does not model is carried as a bare <see cref="Answer"/> instead of failing the read.
/// </param>
/// <param name="RequestId">
/// The request ID echoed in the body, or <c>null</c> when the API reported it only in the
/// <c>x-typesafe-request-id</c> header.
/// </param>
internal sealed record SystemOneBody(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("usage")] Usage Usage,
    [property: JsonPropertyName("answers"), JsonConverter(typeof(AnswerMapConverter))] IReadOnlyDictionary<string, Answer> Answers,
    [property: JsonPropertyName("request_id")] string? RequestId = null);

/// <summary>
/// Answers keyed by question name, grouped by type, with model and usage metadata.
/// See <see href="https://docs.typesafe.ai/concepts/system-one">System One</see> for details.
/// </summary>
public sealed class SystemOneResponse : ApiResponse
{
    private IReadOnlyDictionary<string, NoulAnswer>? _nouls;
    private IReadOnlyDictionary<string, ChoiceAnswer>? _choices;
    private IReadOnlyDictionary<string, ScoreAnswer>? _scores;

    /// <summary>Create a response from its parts.</summary>
    public SystemOneResponse(string model, Usage usage, IReadOnlyDictionary<string, Answer> answers)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Usage = usage ?? throw new ArgumentNullException(nameof(usage));
        Answers = answers ?? throw new ArgumentNullException(nameof(answers));
    }

    /// <summary>The model used to answer the request.</summary>
    public string Model { get; }

    /// <summary>Token usage for the request.</summary>
    public Usage Usage { get; }

    /// <summary>
    /// All answer objects keyed by question name, including any whose type this SDK version does not
    /// model: those arrive as a bare <see cref="Answer"/> carrying the whole payload in
    /// <see cref="Answer.AdditionalProperties"/>, and reach none of <see cref="Nouls"/>,
    /// <see cref="Choices"/> or <see cref="Scores"/>.
    /// </summary>
    public IReadOnlyDictionary<string, Answer> Answers { get; }

    /// <summary>Yes/no answers keyed by question name.</summary>
    public IReadOnlyDictionary<string, NoulAnswer> Nouls => _nouls ??= Filter<NoulAnswer>();

    /// <summary>Choice answers keyed by question name.</summary>
    public IReadOnlyDictionary<string, ChoiceAnswer> Choices => _choices ??= Filter<ChoiceAnswer>();

    /// <summary>Score answers keyed by question name.</summary>
    public IReadOnlyDictionary<string, ScoreAnswer> Scores => _scores ??= Filter<ScoreAnswer>();

    private Dictionary<string, TAnswer> Filter<TAnswer>() where TAnswer : Answer
    {
        var filtered = new Dictionary<string, TAnswer>();
        foreach (var (name, answer) in Answers)
        {
            if (answer is TAnswer typed) filtered[name] = typed;
        }
        return filtered;
    }

    /// <summary>
    /// The answer to a named question, typed by the question: <c>response.Get(tone)</c> is a
    /// <see cref="ChoiceAnswer"/> when <c>tone</c> is a <see cref="Named{TAnswer}"/> of
    /// <see cref="ChoiceAnswer"/>, so no cast and no second spelling of the name is needed.
    /// </summary>
    /// <typeparam name="TAnswer">The answer type the question declares.</typeparam>
    /// <param name="question">The named question, as built by the <see cref="Question.Named"/> builders.</param>
    /// <returns>The answer stored under <see cref="Named{TAnswer}.Name"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="question"/> is <c>null</c>.</exception>
    /// <exception cref="TypeSafeException">
    /// The response carries no answer under that name, or carries one of another type — including an
    /// answer whose type this SDK version does not model, which is reported by its wire type.
    /// </exception>
    /// <exception cref="TypeSafeApiResponseValidationException">
    /// The answer has the type the question declares but carries a value the question cannot map — a
    /// label an enum-typed choice question does not declare — named by
    /// <see cref="TypeSafeApiResponseValidationException.FieldPath"/>, such as <c>answers.tone.choice</c>.
    /// </exception>
    public TAnswer Get<TAnswer>(Named<TAnswer> question) where TAnswer : Answer
    {
        ArgumentNullException.ThrowIfNull(question);
        var name = question.Name;
        if (!Answers.TryGetValue(name, out var answer))
            throw new TypeSafeException($"No answer named \"{name}\" in the response.");
        if (!TryConvert(question, answer, out var converted, out var invalidField))
        {
            throw invalidField is null
                ? new TypeSafeException(Mismatch<TAnswer>(name, answer))
                : Invalid($"answers.{name}.{invalidField}");
        }
        return converted;
    }

    /// <summary>
    /// The non-throwing form of <see cref="Get{TAnswer}(Named{TAnswer})"/>: <c>false</c> when the response
    /// carries no answer under the question's name, when it carries one of another type, and when it
    /// carries one the question cannot map, which <see cref="Get{TAnswer}(Named{TAnswer})"/> reports as
    /// invalid response data.
    /// </summary>
    /// <typeparam name="TAnswer">The answer type the question declares.</typeparam>
    /// <param name="question">The named question, as built by the <see cref="Question.Named"/> builders.</param>
    /// <param name="answer">The typed answer when this returns <c>true</c>; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> when an answer of the declared type is present under the question's name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="question"/> is <c>null</c>.</exception>
    public bool TryGet<TAnswer>(Named<TAnswer> question, [NotNullWhen(true)] out TAnswer? answer)
        where TAnswer : Answer
    {
        ArgumentNullException.ThrowIfNull(question);
        answer = null;
        return Answers.TryGetValue(question.Name, out var found) && TryConvert(question, found, out answer, out _);
    }

    /// <summary>
    /// The single place an <see cref="Answer"/> from <see cref="Answers"/> becomes the answer type a
    /// question declares, so <see cref="Get{TAnswer}(Named{TAnswer})"/> and
    /// <see cref="TryGet{TAnswer}(Named{TAnswer}, out TAnswer)"/> agree on what a hit is. The question
    /// itself does the converting — it is the only object that still knows the type argument of an
    /// enum-typed question, so the mapping needs no reflection — and for every plain question that is
    /// still the runtime type check.
    /// </summary>
    /// <param name="question">The named question the answer was looked up with.</param>
    /// <param name="answer">The answer decoded from the response.</param>
    /// <param name="converted">The typed answer when this returns <c>true</c>; otherwise <c>null</c>.</param>
    /// <param name="invalidField">
    /// The field path within the answer that could not be mapped, or <c>null</c> when the answer is simply
    /// of another type: a failure with a field path is invalid response data, one without is a mismatch.
    /// </param>
    private static bool TryConvert<TAnswer>(
        Named<TAnswer> question, Answer answer, [NotNullWhen(true)] out TAnswer? converted, out string? invalidField)
        where TAnswer : Answer =>
        question.Question.TryConvertAnswer(answer, out converted, out invalidField);

    /// <summary>
    /// The message for an answer present under <paramref name="name"/> but of another type. A bare
    /// <see cref="Answer"/> has no C# type worth naming, so it is reported by the <c>type</c> it arrived with.
    /// </summary>
    private static string Mismatch<TAnswer>(string name, Answer answer) where TAnswer : Answer =>
        answer.GetType() == typeof(Answer)
            ? $"Answer \"{name}\" has the unrecognized type \"{UnmodelledType(answer)}\", not a {typeof(TAnswer).Name}."
            : $"Answer \"{name}\" is a {answer.GetType().Name}, not a {typeof(TAnswer).Name}.";

    /// <summary>
    /// Project a decoded <c>POST /v1/systemone</c> body, warning about answer types this SDK does not model.
    /// </summary>
    /// <param name="body">The body decoded by <c>TypeSafeJsonContext</c>.</param>
    /// <param name="warn">Called once per unmodelled answer, naming the question and the unrecognized type.</param>
    internal static SystemOneResponse FromBody(SystemOneBody body, Action<string>? warn)
    {
        // Forward-compat: an answer whose type this SDK version does not model decodes as a bare
        // Answer, with every field (including "type") in AdditionalProperties. Each one is reported
        // once, then kept in Answers so a caller can inspect what the server sent; the per-type views
        // match on the derived records, so a bare answer reaches none of them. The raw payload also
        // stays available through RawHttpResponse.
        if (warn is not null)
        {
            foreach (var (name, answer) in body.Answers)
            {
                if (answer.GetType() == typeof(Answer))
                {
                    warn($"Ignoring answer \"{name}\" with unrecognized type \"{UnmodelledType(answer)}\"");
                }
            }
        }
        return new SystemOneResponse(body.Model, body.Usage, body.Answers);
    }

    /// <summary>The <c>type</c> a bare answer arrived with, from its extension data, or the empty string.</summary>
    private static string UnmodelledType(Answer answer) =>
        answer.AdditionalProperties is { } fields
        && fields.TryGetValue("type", out var type)
        && type.ValueKind == JsonValueKind.String
            ? type.GetString() ?? ""
            : "";
}

/// <summary>Metadata for an available model.</summary>
/// <param name="Name">The model name, usable as a request's <c>model</c>.</param>
/// <param name="Description">A description of the model.</param>
/// <param name="ReleaseDate">The model's release date, as reported by the API.</param>
public sealed record ModelMetadata(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("release_date")] string ReleaseDate);

/// <summary>
/// The wire shape of a <c>GET /v1/models</c> response body. Decoded by the source-generated
/// <c>TypeSafeJsonContext</c>; <see cref="ListModelsResponse"/> is the public projection of it.
/// </summary>
/// <param name="Models">The models available to the account, in the order the API returned them.</param>
internal sealed record ModelList(
    [property: JsonPropertyName("models")] IReadOnlyList<ModelMetadata> Models);

/// <summary>The models available to the account, as a read-only list of <see cref="ModelMetadata"/>.</summary>
public sealed class ListModelsResponse : ApiResponse, IReadOnlyList<ModelMetadata>
{
    private readonly IReadOnlyList<ModelMetadata> _models;

    /// <summary>Create a model list response.</summary>
    public ListModelsResponse(IReadOnlyList<ModelMetadata> models)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
    }

    /// <summary>The number of available models.</summary>
    public int Count => _models.Count;

    /// <summary>The model at <paramref name="index"/>.</summary>
    /// <param name="index">Zero-based index into the list of available models.</param>
    public ModelMetadata this[int index] => _models[index];

    /// <summary>Enumerate the available models, in the order the API returned them.</summary>
    /// <returns>An enumerator over the available models.</returns>
    public IEnumerator<ModelMetadata> GetEnumerator() => _models.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Project a decoded <c>GET /v1/models</c> body.</summary>
    /// <param name="body">The body decoded by <c>TypeSafeJsonContext</c>.</param>
    internal static ListModelsResponse FromBody(ModelList body) => new(body.Models);
}
