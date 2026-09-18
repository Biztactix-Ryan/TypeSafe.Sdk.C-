using System.Text.Json.Nodes;
using TypeSafe.Internal;

namespace TypeSafe;

/// <summary>
/// A question to ask about a state, identified by its <see cref="Type"/>. Create questions with the
/// static builders <see cref="Noul(object?, object?, object?)"/>, <see cref="Choice(object?, string[])"/>,
/// <see cref="Score(object?, object?[])"/>, or <see cref="FromJson(JsonObject)"/> for raw dictionaries.
/// </summary>
/// <remarks>
/// Instructions and descriptions accept text, a JSON object, or an array. Plain .NET objects
/// (anonymous types, dictionaries, records) are serialized with camelCase web defaults; pass a
/// <see cref="JsonNode"/> to control serialization yourself.
/// </remarks>
public abstract class Question
{
    private protected Question(JsonNode? instructions)
    {
        Instructions = instructions;
    }

    /// <summary>The wire discriminator: <c>noul</c>, <c>choice</c>, or <c>score</c>.</summary>
    public abstract string Type { get; }

    /// <summary>The question as text, a JSON object, or an array; <c>null</c> leaves it unset.</summary>
    public JsonNode? Instructions { get; }

    /// <summary>
    /// Create a yes/no question with optional descriptions for either outcome.
    /// See the <see href="https://docs.typesafe.ai/primitives/noul">noul primitive</see> for details.
    /// </summary>
    /// <param name="instructions">The question as text, a JSON object, or an array; optional.</param>
    /// <param name="whenTrue">Optional description of the yes outcome.</param>
    /// <param name="whenFalse">Optional description of the no outcome.</param>
    public static NoulQuestion Noul(object? instructions = null, object? whenTrue = null, object? whenFalse = null) =>
        new(JsonContent.From(instructions), whenTrue is null && whenFalse is null
            ? null
            : new NoulCriteria(JsonContent.From(whenTrue), JsonContent.From(whenFalse)));

    /// <summary>
    /// Create a question that selects between undescribed labels.
    /// See the <see href="https://docs.typesafe.ai/primitives/choice">choice primitive</see> for details.
    /// </summary>
    /// <param name="instructions">The question as text, a JSON object, or an array; optional.</param>
    /// <param name="labels">The available labels.</param>
    public static ChoiceQuestion Choice(object? instructions, params string[] labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        var criteria = new Dictionary<string, JsonNode?>();
        foreach (var label in labels) criteria[label] = null;
        return new ChoiceQuestion(JsonContent.From(instructions), criteria);
    }

    /// <summary>
    /// Create a question that selects between named alternatives.
    /// See the <see href="https://docs.typesafe.ai/primitives/choice">choice primitive</see> for details.
    /// </summary>
    /// <param name="instructions">The question as text, a JSON object, or an array; optional.</param>
    /// <param name="criteria">Labels mapped to descriptions, or <c>null</c> for undescribed labels.</param>
    public static ChoiceQuestion Choice<TDescription>(object? instructions, IEnumerable<KeyValuePair<string, TDescription>> criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var converted = new Dictionary<string, JsonNode?>();
        foreach (var (label, description) in criteria) converted[label] = JsonContent.From(description);
        return new ChoiceQuestion(JsonContent.From(instructions), converted);
    }

    /// <summary>
    /// Create a question that assigns a score using an ordered rubric.
    /// See the <see href="https://docs.typesafe.ai/primitives/score">score primitive</see> for details.
    /// </summary>
    /// <param name="instructions">The question as text, a JSON object, or an array; optional.</param>
    /// <param name="criteria">Descriptions indexed by score from zero, at least one; entries may be <c>null</c>.</param>
    public static ScoreQuestion Score(object? instructions, params object?[] criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        return new ScoreQuestion(JsonContent.From(instructions), criteria.Select(JsonContent.From).ToList());
    }

    /// <summary>
    /// Create a question that assigns a score using an ordered rubric.
    /// See the <see href="https://docs.typesafe.ai/primitives/score">score primitive</see> for details.
    /// </summary>
    /// <param name="instructions">The question as text, a JSON object, or an array; optional.</param>
    /// <param name="criteria">Descriptions indexed by score from zero, at least one; entries may be <c>null</c>.</param>
    public static ScoreQuestion Score<TDescription>(object? instructions, IEnumerable<TDescription> criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        return new ScoreQuestion(JsonContent.From(instructions), criteria.Select(item => JsonContent.From(item)).ToList());
    }

    /// <summary>
    /// Wrap a raw question object with a nonempty string <c>type</c>, forwarding every field as-is.
    /// Useful for question types or fields this SDK version does not model.
    /// </summary>
    public static RawQuestion FromJson(JsonObject json) => new(json);

    /// <summary>Serialize the question for the request body, validating it under <paramref name="name"/>.</summary>
    internal abstract JsonObject ToJson(string name);

    private protected JsonObject Envelope()
    {
        var json = new JsonObject { ["type"] = Type };
        if (Instructions is not null) json["instructions"] = Instructions.DeepClone();
        return json;
    }

    /// <summary>Validate a set of questions and serialize it for the request body.</summary>
    internal static JsonObject Normalize(IEnumerable<KeyValuePair<string, Question>> questions)
    {
        ArgumentNullException.ThrowIfNull(questions);
        var json = new JsonObject();
        foreach (var (name, question) in questions)
        {
            if (question is null) throw new TypeSafeException($"Question \"{name}\" must not be null.");
            json[name] = question.ToJson(name);
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
public sealed class NoulCriteria
{
    /// <summary>Create outcome descriptions; <c>null</c> leaves an outcome undescribed.</summary>
    public NoulCriteria(JsonNode? whenTrue = null, JsonNode? whenFalse = null)
    {
        WhenTrue = whenTrue;
        WhenFalse = whenFalse;
    }

    /// <summary>Description of the yes outcome as text, a JSON object, or an array; <c>null</c> leaves it undescribed.</summary>
    public JsonNode? WhenTrue { get; }

    /// <summary>Description of the no outcome as text, a JSON object, or an array; <c>null</c> leaves it undescribed.</summary>
    public JsonNode? WhenFalse { get; }

    internal JsonObject ToJson()
    {
        var json = new JsonObject();
        if (WhenTrue is not null) json["true"] = WhenTrue.DeepClone();
        if (WhenFalse is not null) json["false"] = WhenFalse.DeepClone();
        return json;
    }
}

/// <summary>A yes/no question with optional descriptions for either outcome.</summary>
public sealed class NoulQuestion : Question
{
    /// <summary>Create a yes/no question.</summary>
    public NoulQuestion(JsonNode? instructions = null, NoulCriteria? criteria = null) : base(instructions)
    {
        Criteria = criteria;
    }

    /// <inheritdoc/>
    public override string Type => "noul";

    /// <summary>Optional descriptions of the yes and no outcomes.</summary>
    public NoulCriteria? Criteria { get; }

    internal override JsonObject ToJson(string name)
    {
        var json = Envelope();
        if (Criteria is not null) json["criteria"] = Criteria.ToJson();
        return json;
    }
}

/// <summary>A question that selects between named alternatives.</summary>
public sealed class ChoiceQuestion : Question
{
    /// <summary>Create a choice question from labels mapped to descriptions, or <c>null</c> for undescribed labels.</summary>
    public ChoiceQuestion(JsonNode? instructions, IReadOnlyDictionary<string, JsonNode?> criteria) : base(instructions)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        Criteria = criteria;
    }

    /// <inheritdoc/>
    public override string Type => "choice";

    /// <summary>Labels mapped to descriptions, or <c>null</c> for undescribed labels.</summary>
    public IReadOnlyDictionary<string, JsonNode?> Criteria { get; }

    internal override JsonObject ToJson(string name)
    {
        var json = Envelope();
        var criteria = new JsonObject();
        foreach (var (label, description) in Criteria) criteria[label] = description?.DeepClone();
        json["criteria"] = criteria;
        return json;
    }
}

/// <summary>A question that assigns a score using an ordered rubric.</summary>
public sealed class ScoreQuestion : Question
{
    /// <summary>Create a score question from descriptions indexed by score from zero.</summary>
    public ScoreQuestion(JsonNode? instructions, IReadOnlyList<JsonNode?> criteria) : base(instructions)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        Criteria = criteria;
    }

    /// <inheritdoc/>
    public override string Type => "score";

    /// <summary>Ordered descriptions, one per score from zero; entries may be <c>null</c>.</summary>
    public IReadOnlyList<JsonNode?> Criteria { get; }

    internal override JsonObject ToJson(string name)
    {
        ValidateScoreCriteria(name, Criteria.Count);
        var json = Envelope();
        var criteria = new JsonArray();
        foreach (var description in Criteria) criteria.Add(description?.DeepClone());
        json["criteria"] = criteria;
        return json;
    }
}

/// <summary>A question supplied as a raw JSON object, forwarded with every field intact.</summary>
public sealed class RawQuestion : Question
{
    /// <summary>Wrap a raw question object; it must carry a nonempty string <c>type</c>.</summary>
    public RawQuestion(JsonObject json) : base(json?["instructions"]?.DeepClone())
    {
        ArgumentNullException.ThrowIfNull(json);
        Json = json;
    }

    /// <inheritdoc/>
    public override string Type => JsonContent.AsString(Json["type"]) ?? "";

    /// <summary>The raw question object.</summary>
    public JsonObject Json { get; }

    internal override JsonObject ToJson(string name)
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
