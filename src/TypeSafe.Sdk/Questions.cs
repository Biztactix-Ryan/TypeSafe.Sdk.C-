using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TypeSafe.Internal;

namespace TypeSafe;

/// <summary>
/// A question to ask about a state, identified by its <see cref="Type"/>. Create questions with the
/// static builders <see cref="Noul(Content, Content, Content)"/>, <see cref="Choice(Content, string[])"/>,
/// <see cref="Score(Content, Content[])"/>, or <see cref="FromJson(JsonObject)"/> for raw dictionaries.
/// </summary>
/// <remarks>
/// <para>
/// Instructions and descriptions are <see cref="Content"/>: text, a <see cref="JsonObject"/>, and a
/// <see cref="JsonArray"/> convert implicitly, and the default value leaves them unset. Plain .NET
/// objects (anonymous types, dictionaries, records) go through <see cref="Content.From(object?)"/>,
/// which serializes them with camelCase web defaults.
/// </para>
/// <para>
/// The question records are written by the source-generated <c>TypeSafeJsonContext</c>: <c>type</c> is
/// the polymorphic discriminator, so it is always written first and the <see cref="Type"/> property
/// itself is ignored, and <c>[JsonPropertyOrder]</c> pins <c>instructions</c> ahead of <c>criteria</c>
/// regardless of where in the hierarchy each is declared. <see cref="RawQuestion"/> is deliberately not
/// a derived type of this hierarchy: it forwards its own JSON object instead.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(NoulQuestion), "noul")]
[JsonDerivedType(typeof(ChoiceQuestion), "choice")]
[JsonDerivedType(typeof(ScoreQuestion), "score")]
public abstract record Question
{
    private protected Question(JsonNode? instructions)
    {
        Instructions = instructions;
    }

    /// <summary>
    /// The wire discriminator: <c>noul</c>, <c>choice</c>, or <c>score</c>. Always ignored by the
    /// serializer: <c>type</c> is polymorphic metadata, written by the discriminator.
    /// </summary>
    [JsonIgnore]
    public abstract string Type { get; }

    /// <summary>The question as text, a JSON object, or an array; <c>null</c> leaves it unset.</summary>
    [JsonPropertyName("instructions")]
    [JsonPropertyOrder(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? Instructions { get; }

    /// <summary>
    /// Create a yes/no question with optional descriptions for either outcome.
    /// See the <see href="https://docs.typesafe.ai/primitives/noul">noul primitive</see> for details.
    /// </summary>
    /// <param name="instructions">The question as text, a JSON object, or an array; optional.</param>
    /// <param name="whenTrue">Optional description of the yes outcome.</param>
    /// <param name="whenFalse">Optional description of the no outcome.</param>
    public static NoulQuestion Noul(Content instructions = default, Content whenTrue = default, Content whenFalse = default) =>
        new(instructions.Node, whenTrue.Node is null && whenFalse.Node is null
            ? null
            : new NoulCriteria(whenTrue, whenFalse));

    /// <summary>
    /// Create a question that selects between undescribed labels.
    /// See the <see href="https://docs.typesafe.ai/primitives/choice">choice primitive</see> for details.
    /// </summary>
    /// <param name="instructions">The question as text, a JSON object, or an array; optional.</param>
    /// <param name="labels">The available labels.</param>
    public static ChoiceQuestion Choice(Content instructions, params string[] labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        var criteria = new Dictionary<string, JsonNode?>();
        foreach (var label in labels) criteria[label] = null;
        return new ChoiceQuestion(instructions.Node, criteria);
    }

    /// <summary>
    /// Create a question that selects between named alternatives.
    /// See the <see href="https://docs.typesafe.ai/primitives/choice">choice primitive</see> for details.
    /// </summary>
    /// <param name="instructions">The question as text, a JSON object, or an array; optional.</param>
    /// <param name="criteria">Labels mapped to descriptions; the default <see cref="Content"/> leaves a label undescribed.</param>
    public static ChoiceQuestion Choice(Content instructions, IEnumerable<KeyValuePair<string, Content>> criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var converted = new Dictionary<string, JsonNode?>();
        foreach (var (label, description) in criteria) converted[label] = description.Node;
        return new ChoiceQuestion(instructions.Node, converted);
    }

    /// <summary>
    /// Create a question that assigns a score using an ordered rubric.
    /// See the <see href="https://docs.typesafe.ai/primitives/score">score primitive</see> for details.
    /// </summary>
    /// <param name="instructions">The question as text, a JSON object, or an array; optional.</param>
    /// <param name="criteria">
    /// Descriptions indexed by score from zero, at least one; the default <see cref="Content"/> leaves a
    /// score undescribed.
    /// </param>
    public static ScoreQuestion Score(Content instructions, params Content[] criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var descriptions = new List<JsonNode?>(criteria.Length);
        foreach (var description in criteria) descriptions.Add(description.Node);
        return new ScoreQuestion(instructions.Node, descriptions);
    }

    /// <summary>
    /// Wrap a raw question object with a nonempty string <c>type</c>, forwarding every field as-is.
    /// Useful for question types or fields this SDK version does not model.
    /// </summary>
    public static RawQuestion FromJson(JsonObject json) => new(json);

    /// <summary>
    /// Validate a question under <paramref name="name"/> and serialize it for the request body through
    /// the source-generated context. A <see cref="RawQuestion"/> forwards its own JSON object instead.
    /// </summary>
    internal static JsonObject Serialize(string name, Question question)
    {
        ArgumentNullException.ThrowIfNull(question);
        switch (question)
        {
            case RawQuestion raw:
                return raw.Validated(name);
            case ScoreQuestion score:
                ValidateScoreCriteria(name, score.Criteria.Count);
                break;
        }
        return (JsonObject)JsonSerializer.SerializeToNode(question, TypeSafeJsonContext.Default.Question)!;
    }

    /// <summary>Validate a set of questions and serialize it for the request body.</summary>
    internal static JsonObject Normalize(IEnumerable<KeyValuePair<string, Question>> questions)
    {
        ArgumentNullException.ThrowIfNull(questions);
        var json = new JsonObject();
        foreach (var (name, question) in questions)
        {
            if (question is null) throw new TypeSafeException($"Question \"{name}\" must not be null.");
            json[name] = Serialize(name, question);
        }
        if (json.Count == 0) throw new TypeSafeException("At least one question is required.");
        return json;
    }

    private protected static void ValidateScoreCriteria(string name, int count)
    {
        if (count == 0)
            throw new TypeSafeException($"Score question \"{name}\" has no criteria; at least one score is required.");
    }
}

/// <summary>Optional descriptions of the yes and no outcomes of a <see cref="NoulQuestion"/>.</summary>
public sealed record NoulCriteria
{
    /// <summary>Create outcome descriptions; the default <see cref="Content"/> leaves an outcome undescribed.</summary>
    /// <param name="whenTrue">Description of the yes outcome.</param>
    /// <param name="whenFalse">Description of the no outcome.</param>
    public NoulCriteria(Content whenTrue = default, Content whenFalse = default)
    {
        WhenTrue = whenTrue.Node;
        WhenFalse = whenFalse.Node;
    }

    /// <summary>Description of the yes outcome as text, a JSON object, or an array; <c>null</c> leaves it undescribed.</summary>
    [JsonPropertyName("true")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? WhenTrue { get; init; }

    /// <summary>Description of the no outcome as text, a JSON object, or an array; <c>null</c> leaves it undescribed.</summary>
    [JsonPropertyName("false")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? WhenFalse { get; init; }
}

/// <summary>A yes/no question with optional descriptions for either outcome.</summary>
/// <param name="Instructions">The question as text, a JSON object, or an array; <c>null</c> leaves it unset.</param>
/// <param name="Criteria">Optional descriptions of the yes and no outcomes.</param>
public sealed record NoulQuestion(
    JsonNode? Instructions = null,
    [property: JsonPropertyName("criteria")]
    [property: JsonPropertyOrder(2)]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    NoulCriteria? Criteria = null) : Question(Instructions)
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override string Type => "noul";
}

/// <summary>A question that selects between named alternatives.</summary>
/// <param name="Instructions">The question as text, a JSON object, or an array; <c>null</c> leaves it unset.</param>
/// <param name="Criteria">Labels mapped to descriptions, or <c>null</c> for undescribed labels.</param>
public sealed record ChoiceQuestion(
    JsonNode? Instructions,
    IReadOnlyDictionary<string, JsonNode?> Criteria) : Question(Instructions)
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override string Type => "choice";

    /// <summary>Labels mapped to descriptions, or <c>null</c> for undescribed labels.</summary>
    [JsonPropertyName("criteria")]
    [JsonPropertyOrder(2)]
    public IReadOnlyDictionary<string, JsonNode?> Criteria { get; init; } =
        Criteria ?? throw new ArgumentNullException(nameof(Criteria));
}

/// <summary>A question that assigns a score using an ordered rubric.</summary>
/// <param name="Instructions">The question as text, a JSON object, or an array; <c>null</c> leaves it unset.</param>
/// <param name="Criteria">Ordered descriptions, one per score from zero; entries may be <c>null</c>.</param>
public sealed record ScoreQuestion(
    JsonNode? Instructions,
    IReadOnlyList<JsonNode?> Criteria) : Question(Instructions)
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override string Type => "score";

    /// <summary>Ordered descriptions, one per score from zero; entries may be <c>null</c>.</summary>
    [JsonPropertyName("criteria")]
    [JsonPropertyOrder(2)]
    public IReadOnlyList<JsonNode?> Criteria { get; init; } =
        Criteria ?? throw new ArgumentNullException(nameof(Criteria));
}

/// <summary>A question supplied as a raw JSON object, forwarded with every field intact.</summary>
public sealed record RawQuestion : Question
{
    /// <summary>Wrap a raw question object; it must carry a nonempty string <c>type</c>.</summary>
    public RawQuestion(JsonObject json) : base(json?["instructions"]?.DeepClone())
    {
        ArgumentNullException.ThrowIfNull(json);
        Json = json;
    }

    /// <inheritdoc/>
    [JsonIgnore]
    public override string Type => JsonContent.AsString(Json["type"]) ?? "";

    /// <summary>The raw question object.</summary>
    [JsonIgnore]
    public JsonObject Json { get; }

    /// <summary>Validate the raw object under <paramref name="name"/> and return a detached copy of it.</summary>
    internal JsonObject Validated(string name)
    {
        var type = JsonContent.AsString(Json["type"]);
        if (string.IsNullOrEmpty(type))
            throw new TypeSafeException($"Question \"{name}\" must be a question object or a dictionary with a nonempty string \"type\".");
        if (type is "choice" or "score" && !Json.ContainsKey("criteria"))
            throw new TypeSafeException($"Question \"{name}\" requires \"criteria\".");
        if (type == "score")
        {
            if (Json["criteria"] is not JsonArray list)
                throw new TypeSafeException(
                    $"Score question \"{name}\" has criteria that are not a list; score criteria must be a list of descriptions indexed by score from zero.");
            ValidateScoreCriteria(name, list.Count);
        }
        return (JsonObject)Json.DeepClone();
    }
}

/// <summary>Questions keyed by the names used to identify their answers.</summary>
public sealed class Questions : Dictionary<string, Question>
{
    /// <summary>Create an empty question set.</summary>
    public Questions() { }

    /// <summary>Create a question set from existing entries.</summary>
    public Questions(IEnumerable<KeyValuePair<string, Question>> questions) : base(questions) { }
}
