using System.Diagnostics.CodeAnalysis;
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
    /// The question that reaches the wire, which is this question itself for every record the
    /// source-generated context knows. A question modelled with a type argument — <see cref="ChoiceQuestion{TEnum}"/>
    /// or <see cref="ScoreQuestion{TEnum}"/>, whose open generic form can carry no
    /// <c>[JsonDerivedType]</c> and so cannot be registered —
    /// projects itself onto the plain record it is equivalent to, so <see cref="Serialize"/> only ever
    /// serializes a registered type and the wire shape is unchanged.
    /// </summary>
    internal virtual Question ToWire() => this;

    /// <summary>
    /// The name-first question builders: each one takes the name the answer is read with and returns a
    /// <see cref="Named{TAnswer}"/> carrying that name together with the typed question, so the question
    /// object itself can be the key — <c>Question.Named.Noul("billing", "Is this a billing issue?")</c>.
    /// The positional builders on <see cref="Question"/> stay for the dictionary path.
    /// </summary>
    /// <remarks>
    /// The name-first builders live behind <c>Question.Named</c> instead of overloading the positional
    /// ones deliberately. A string argument binds to a <c>string name</c> parameter by an identity
    /// conversion, which is better than the user-defined conversion to <see cref="Content"/>, so a
    /// name-first overload sitting beside <see cref="Question.Choice(Content, string[])"/> would silently re-read
    /// <c>Question.Choice("Tone?", "calm", "angry")</c> as a name, instructions and a single label.
    /// Members of two different types never overload each other, so both families keep their meaning.
    /// </remarks>
    public static class Named
    {
        /// <summary>
        /// Name a yes/no question with optional descriptions for either outcome.
        /// See the <see href="https://docs.typesafe.ai/primitives/noul">noul primitive</see> for details.
        /// </summary>
        /// <param name="name">The name the answer is read with; must not be empty or whitespace.</param>
        /// <param name="instructions">The question as text, a JSON object, or an array; optional.</param>
        /// <param name="whenTrue">Optional description of the yes outcome.</param>
        /// <param name="whenFalse">Optional description of the no outcome.</param>
        /// <exception cref="TypeSafeException"><paramref name="name"/> is empty or whitespace.</exception>
        public static Named<NoulAnswer> Noul(
            string name, Content instructions = default, Content whenTrue = default, Content whenFalse = default) =>
            new(name, Question.Noul(instructions, whenTrue, whenFalse));

        /// <summary>
        /// Name a question that selects between undescribed labels.
        /// See the <see href="https://docs.typesafe.ai/primitives/choice">choice primitive</see> for details.
        /// </summary>
        /// <param name="name">The name the answer is read with; must not be empty or whitespace.</param>
        /// <param name="instructions">The question as text, a JSON object, or an array; optional.</param>
        /// <param name="labels">The available labels.</param>
        /// <exception cref="TypeSafeException"><paramref name="name"/> is empty or whitespace.</exception>
        public static Named<ChoiceAnswer> Choice(string name, Content instructions, params string[] labels) =>
            new(name, Question.Choice(instructions, labels));

        /// <summary>
        /// Name a question that selects between named alternatives.
        /// See the <see href="https://docs.typesafe.ai/primitives/choice">choice primitive</see> for details.
        /// </summary>
        /// <param name="name">The name the answer is read with; must not be empty or whitespace.</param>
        /// <param name="instructions">The question as text, a JSON object, or an array; optional.</param>
        /// <param name="criteria">Labels mapped to descriptions; the default <see cref="Content"/> leaves a label undescribed.</param>
        /// <exception cref="TypeSafeException"><paramref name="name"/> is empty or whitespace.</exception>
        public static Named<ChoiceAnswer> Choice(
            string name, Content instructions, IEnumerable<KeyValuePair<string, Content>> criteria) =>
            new(name, Question.Choice(instructions, criteria));

        /// <summary>
        /// Name a question that selects between the members of <typeparamref name="TEnum"/>, answered with the
        /// enum itself: the labels are the members' wire labels and <c>response.Get(tone).Choice</c> is a
        /// <typeparamref name="TEnum"/>, so no label is ever spelled twice.
        /// See the <see href="https://docs.typesafe.ai/primitives/choice">choice primitive</see> for details.
        /// </summary>
        /// <typeparam name="TEnum">
        /// The enum whose members are the available labels: a member's label is its
        /// <c>[JsonStringEnumMemberName]</c> when it carries one and its snake_case name otherwise, and its
        /// <c>[System.ComponentModel.Description]</c>, when present, describes it to the model.
        /// </typeparam>
        /// <param name="name">The name the answer is read with; must not be empty or whitespace.</param>
        /// <param name="instructions">The question as text, a JSON object, or an array; optional.</param>
        /// <exception cref="TypeSafeException">
        /// <paramref name="name"/> is empty or whitespace, or two members of <typeparamref name="TEnum"/>
        /// map to the same label.
        /// </exception>
        public static Named<ChoiceAnswer<TEnum>> Choice<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(
            string name, Content instructions = default)
            where TEnum : struct, Enum =>
            new(name, new ChoiceQuestion<TEnum>(instructions));

        /// <summary>
        /// Name a question that assigns a score using an ordered rubric.
        /// See the <see href="https://docs.typesafe.ai/primitives/score">score primitive</see> for details.
        /// </summary>
        /// <param name="name">The name the answer is read with; must not be empty or whitespace.</param>
        /// <param name="instructions">The question as text, a JSON object, or an array; optional.</param>
        /// <param name="criteria">
        /// Descriptions indexed by score from zero, at least one; the default <see cref="Content"/> leaves a
        /// score undescribed.
        /// </param>
        /// <exception cref="TypeSafeException"><paramref name="name"/> is empty or whitespace.</exception>
        public static Named<ScoreAnswer> Score(string name, Content instructions, params Content[] criteria) =>
            new(name, Question.Score(instructions, criteria));

        /// <summary>
        /// Name a question that assigns a score using <typeparamref name="TEnum"/> as the rubric, answered
        /// with the enum itself: the criteria are the members' descriptions in value order and
        /// <c>response.Get(urgency).Nearest</c> is a <typeparamref name="TEnum"/>, so no rubric level is
        /// ever spelled twice.
        /// See the <see href="https://docs.typesafe.ai/primitives/score">score primitive</see> for details.
        /// </summary>
        /// <typeparam name="TEnum">
        /// The enum whose members are the rubric levels, lowest first: its values must be exactly
        /// <c>0</c> to <c>N-1</c> in declaration order, and a member is described to the model by its
        /// <c>[System.ComponentModel.Description]</c>, falling back to its wire label.
        /// </typeparam>
        /// <param name="name">The name the answer is read with; must not be empty or whitespace.</param>
        /// <param name="instructions">The question as text, a JSON object, or an array; optional.</param>
        /// <exception cref="TypeSafeException">
        /// <paramref name="name"/> is empty or whitespace, <typeparamref name="TEnum"/> declares no members
        /// or values that are not contiguous from zero, or two of its members map to the same label.
        /// </exception>
        public static Named<ScoreAnswer<TEnum>> Score<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(
            string name, Content instructions = default)
            where TEnum : struct, Enum =>
            new(name, new ScoreQuestion<TEnum>(instructions));
    }

    /// <summary>
    /// Validate a question under <paramref name="name"/> and serialize it for the request body through
    /// the source-generated context. A <see cref="RawQuestion"/> forwards its own JSON object instead.
    /// </summary>
    internal static JsonObject Serialize(string name, Question question)
    {
        ArgumentNullException.ThrowIfNull(question);
        var wire = question.ToWire();
        switch (wire)
        {
            case RawQuestion raw:
                return raw.Validated(name);
            case ScoreQuestion score:
                ValidateScoreCriteria(name, score.Criteria.Count);
                break;
        }
        return (JsonObject)JsonSerializer.SerializeToNode(wire, TypeSafeJsonContext.Default.Question)!;
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

/// <summary>
/// A <see cref="Question"/> that declares the answer type it produces, so a caller can name the answer
/// type without repeating the question type: <c>Question&lt;NoulAnswer&gt; q = Question.Noul("...");</c>.
/// </summary>
/// <typeparam name="TAnswer">The <see cref="Answer"/> type this question is answered with.</typeparam>
/// <remarks>
/// Purely a type-level marker: it adds no serialized members, so the wire shape is that of
/// <see cref="Question"/> and the <c>[JsonPolymorphic]</c> metadata stays on the non-generic base.
/// <see cref="RawQuestion"/> derives from <see cref="Question"/> directly: its answer type is whatever
/// the raw <c>type</c> maps to and is not known at compile time.
/// </remarks>
public abstract record Question<TAnswer> : Question
    where TAnswer : Answer
{
    private protected Question(JsonNode? instructions) : base(instructions)
    {
    }

    /// <summary>
    /// Turn the answer the response carries under this question's name into the answer type the question
    /// declares. The default is the runtime type check <see cref="SystemOneResponse.Get{TAnswer}(Named{TAnswer})"/>
    /// has always done; <see cref="ChoiceQuestion{TEnum}"/> and <see cref="ScoreQuestion{TEnum}"/> override
    /// it to map the wire labels and rubric levels onto their enum.
    /// </summary>
    /// <param name="answer">The answer decoded from the response.</param>
    /// <param name="converted">The typed answer when this returns <c>true</c>; otherwise <c>null</c>.</param>
    /// <param name="invalidField">
    /// When the answer has the right shape but carries a value this question cannot map, the field path
    /// relative to the answer (<c>choice</c>, <c>probabilities.furious</c>); <c>null</c> for a plain type
    /// mismatch, which is reported as one instead.
    /// </param>
    /// <returns><c>true</c> when the answer became a <typeparamref name="TAnswer"/>.</returns>
    internal virtual bool TryConvertAnswer(
        Answer answer, [NotNullWhen(true)] out TAnswer? converted, out string? invalidField)
    {
        invalidField = null;
        converted = answer as TAnswer;
        return converted is not null;
    }
}

/// <summary>
/// A question together with the name its answer is read with, without naming the answer type: the
/// non-generic view of <see cref="Named{TAnswer}"/>, so a heterogeneous set of named questions can be
/// held in one collection.
/// </summary>
public interface INamedQuestion
{
    /// <summary>The name the answer is read with; never empty.</summary>
    string Name { get; }

    /// <summary>The question asked under <see cref="Name"/>.</summary>
    Question Question { get; }
}

/// <summary>
/// A <see cref="Question{TAnswer}"/> paired with the name its answer is read with, as returned by the
/// name-first builders on <see cref="TypeSafe.Question.Named"/>:
/// <c>Named&lt;NoulAnswer&gt; billing = Question.Named.Noul("billing", "Is this a billing issue?");</c>.
/// </summary>
/// <typeparam name="TAnswer">The <see cref="Answer"/> type the question is answered with.</typeparam>
/// <param name="Name">The name the answer is read with; must not be empty or whitespace.</param>
/// <param name="Question">The question asked under <paramref name="Name"/>.</param>
/// <remarks>
/// Nothing here reaches the wire: the name becomes the key of the question in the request body and the
/// question serializes exactly as it does on its own.
/// </remarks>
public sealed record Named<TAnswer>(string Name, Question<TAnswer> Question) : INamedQuestion
    where TAnswer : Answer
{
    private readonly string _name = ValidateName(Name);
    private readonly Question<TAnswer> _question = Question ?? throw new ArgumentNullException(nameof(Question));

    /// <summary>The name the answer is read with; never empty.</summary>
    /// <exception cref="TypeSafeException">The name is empty or whitespace.</exception>
    public string Name
    {
        get => _name;
        init => _name = ValidateName(value);
    }

    /// <summary>The question asked under <see cref="Name"/>.</summary>
    /// <exception cref="ArgumentNullException">The question is <c>null</c>.</exception>
    public Question<TAnswer> Question
    {
        get => _question;
        init => _question = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <inheritdoc/>
    Question INamedQuestion.Question => Question;

    private static string ValidateName(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? throw new TypeSafeException("Question name must not be empty.")
            : name;
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
    NoulCriteria? Criteria = null) : Question<NoulAnswer>(Instructions)
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
    IReadOnlyDictionary<string, JsonNode?> Criteria) : Question<ChoiceAnswer>(Instructions)
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

/// <summary>
/// A question that selects between the members of <typeparamref name="TEnum"/>, answered with a
/// <see cref="ChoiceAnswer{TEnum}"/>. Build one with <see cref="Question.Named"/>:
/// <c>Question.Named.Choice&lt;Tone&gt;("tone", "What is the tone?")</c>.
/// </summary>
/// <typeparam name="TEnum">
/// The enum whose members are the available labels: a member's label is its
/// <c>[JsonStringEnumMemberName]</c> when it carries one and its snake_case name otherwise, and its
/// <c>[System.ComponentModel.Description]</c>, when present, describes it to the model.
/// </typeparam>
/// <remarks>
/// The wire shape is exactly that of <see cref="ChoiceQuestion"/> — the same <c>"type":"choice"</c> and
/// the same criteria object — so nothing about the request changes. An open generic can carry no
/// <c>[JsonDerivedType]</c> and cannot be registered with the source-generated context, so this record is
/// never serialized itself: <see cref="ToWire"/> projects it onto a <see cref="ChoiceQuestion"/> holding
/// the very same criteria, and <c>Question.Serialize</c> writes that (see
/// <c>.project/DECISIONS.md</c>).
/// </remarks>
public sealed record ChoiceQuestion<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>
    : Question<ChoiceAnswer<TEnum>>
    where TEnum : struct, Enum
{
    /// <summary>Ask which member of <typeparamref name="TEnum"/> applies.</summary>
    /// <param name="instructions">The question as text, a JSON object, or an array; optional.</param>
    /// <exception cref="TypeSafeException">Two members of <typeparamref name="TEnum"/> map to the same label.</exception>
    public ChoiceQuestion(Content instructions = default) : base(instructions.Node)
    {
        var criteria = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var entry in EnumLabels<TEnum>.Entries)
        {
            criteria[entry.Label] = entry.Description is null ? null : JsonValue.Create(entry.Description);
        }
        Criteria = criteria;
    }

    /// <inheritdoc/>
    [JsonIgnore]
    public override string Type => "choice";

    /// <summary>
    /// The enum's labels in declaration order, each mapped to the member's <c>[Description]</c> or to
    /// <c>null</c> when it carries none.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyDictionary<string, JsonNode?> Criteria { get; }

    /// <inheritdoc/>
    internal override Question ToWire() => new ChoiceQuestion(Instructions, Criteria);

    /// <inheritdoc/>
    internal override bool TryConvertAnswer(
        Answer answer, [NotNullWhen(true)] out ChoiceAnswer<TEnum>? converted, out string? invalidField)
    {
        invalidField = null;
        switch (answer)
        {
            case ChoiceAnswer<TEnum> already:
                converted = already;
                return true;
            case ChoiceAnswer wire:
                return TryConvertChoice(wire, out converted, out invalidField);
            default:
                converted = null;
                return false;
        }
    }

    /// <summary>
    /// Map a wire <see cref="ChoiceAnswer"/> onto <typeparamref name="TEnum"/>. Every label the server
    /// sent must be one the enum declares — the selected one and every probability key — so a label the
    /// enum does not know is reported as invalid response data rather than silently dropped.
    /// </summary>
    private static bool TryConvertChoice(
        ChoiceAnswer wire, [NotNullWhen(true)] out ChoiceAnswer<TEnum>? converted, out string? invalidField)
    {
        converted = null;
        if (!EnumLabels<TEnum>.TryGetMember(wire.Choice, out var choice))
        {
            invalidField = "choice";
            return false;
        }
        if (wire.Probabilities is not { } wireProbabilities)
        {
            invalidField = "probabilities";
            return false;
        }

        var probabilities = new Dictionary<TEnum, double>(wireProbabilities.Count);
        foreach (var (label, probability) in wireProbabilities)
        {
            if (!EnumLabels<TEnum>.TryGetMember(label, out var member))
            {
                invalidField = $"probabilities.{label}";
                return false;
            }
            probabilities[member] = probability;
        }

        invalidField = null;
        converted = new ChoiceAnswer<TEnum>(choice, wire.Confidence, probabilities)
        {
            AdditionalProperties = wire.AdditionalProperties,
        };
        return true;
    }
}

/// <summary>A question that assigns a score using an ordered rubric.</summary>
/// <param name="Instructions">The question as text, a JSON object, or an array; <c>null</c> leaves it unset.</param>
/// <param name="Criteria">Ordered descriptions, one per score from zero; entries may be <c>null</c>.</param>
public sealed record ScoreQuestion(
    JsonNode? Instructions,
    IReadOnlyList<JsonNode?> Criteria) : Question<ScoreAnswer>(Instructions)
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

/// <summary>
/// A question that assigns a score using the members of <typeparamref name="TEnum"/> as the rubric,
/// answered with a <see cref="ScoreAnswer{TEnum}"/>. Build one with <see cref="Question.Named"/>:
/// <c>Question.Named.Score&lt;Urgency&gt;("urgency", "How urgent?")</c>.
/// </summary>
/// <typeparam name="TEnum">
/// The enum whose members are the rubric levels, lowest first: its values must be exactly <c>0</c> to
/// <c>N-1</c> in declaration order, and a member is described to the model by its
/// <c>[System.ComponentModel.Description]</c>, falling back to its wire label.
/// </typeparam>
/// <remarks>
/// The wire shape is exactly that of <see cref="ScoreQuestion"/> — the same <c>"type":"score"</c> and the
/// same ordered criteria — so nothing about the request changes. Like <see cref="ChoiceQuestion{TEnum}"/>,
/// an open generic can carry no <c>[JsonDerivedType]</c> and cannot be registered with the
/// source-generated context, so this record is never serialized itself: <see cref="ToWire"/> projects it
/// onto a <see cref="ScoreQuestion"/> holding the very same criteria, and <c>Question.Serialize</c> writes
/// that (see <c>.project/DECISIONS.md</c>).
/// </remarks>
public sealed record ScoreQuestion<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>
    : Question<ScoreAnswer<TEnum>>
    where TEnum : struct, Enum
{
    /// <summary>Ask for a score on the rubric <typeparamref name="TEnum"/> spells out.</summary>
    /// <param name="instructions">The question as text, a JSON object, or an array; optional.</param>
    /// <exception cref="TypeSafeException">
    /// <typeparamref name="TEnum"/> declares no members or values that are not contiguous from zero, or
    /// two of its members map to the same label.
    /// </exception>
    public ScoreQuestion(Content instructions = default) : base(instructions.Node)
    {
        var entries = EnumLabels<TEnum>.Entries;
        Rubric.Validate(entries);
        var criteria = new JsonNode?[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            criteria[i] = JsonValue.Create(entries[i].Description ?? entries[i].Label);
        }
        Criteria = criteria;
    }

    /// <inheritdoc/>
    [JsonIgnore]
    public override string Type => "score";

    /// <summary>
    /// The rubric in value order, one entry per member: the member's <c>[Description]</c>, or its wire
    /// label when it carries none.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<JsonNode?> Criteria { get; }

    /// <inheritdoc/>
    internal override Question ToWire() => new ScoreQuestion(Instructions, Criteria);

    /// <inheritdoc/>
    internal override bool TryConvertAnswer(
        Answer answer, [NotNullWhen(true)] out ScoreAnswer<TEnum>? converted, out string? invalidField)
    {
        invalidField = null;
        switch (answer)
        {
            case ScoreAnswer<TEnum> already:
                converted = already;
                return true;
            case ScoreAnswer wire:
                return TryConvertScore(wire, out converted, out invalidField);
            default:
                converted = null;
                return false;
        }
    }

    /// <summary>
    /// Map a wire <see cref="ScoreAnswer"/> onto <typeparamref name="TEnum"/>. Every integer key the
    /// server sent must name a rubric level the enum declares — in the legend and in the probabilities —
    /// so a level outside the rubric is reported as invalid response data rather than silently dropped.
    /// </summary>
    private static bool TryConvertScore(
        ScoreAnswer wire, [NotNullWhen(true)] out ScoreAnswer<TEnum>? converted, out string? invalidField)
    {
        converted = null;
        if (wire.Legend is not { } wireLegend)
        {
            invalidField = "legend";
            return false;
        }
        if (wire.Probabilities is not { } wireProbabilities)
        {
            invalidField = "probabilities";
            return false;
        }

        var legend = new Dictionary<TEnum, JsonNode?>(wireLegend.Count);
        foreach (var (score, description) in wireLegend)
        {
            if (!Rubric.TryGetLevel<TEnum>(score, out var level))
            {
                invalidField = $"legend.{score}";
                return false;
            }
            legend[level] = description;
        }

        var probabilities = new Dictionary<TEnum, double>(wireProbabilities.Count);
        foreach (var (score, probability) in wireProbabilities)
        {
            if (!Rubric.TryGetLevel<TEnum>(score, out var level))
            {
                invalidField = $"probabilities.{score}";
                return false;
            }
            probabilities[level] = probability;
        }

        invalidField = null;
        converted = new ScoreAnswer<TEnum>(wire.Score, wire.Confidence, legend, probabilities)
        {
            AdditionalProperties = wire.AdditionalProperties,
        };
        return true;
    }
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
