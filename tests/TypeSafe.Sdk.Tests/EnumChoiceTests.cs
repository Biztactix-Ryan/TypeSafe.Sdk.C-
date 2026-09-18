using System.ComponentModel;
using System.Net;
using System.Text.Json.Serialization;

namespace TypeSafe.Tests;

/// <summary>
/// Enum-typed choice questions: the wire shape is a plain choice question built from the enum's labels,
/// and the answer comes back as the enum itself, with a label the enum does not declare reported as
/// invalid response data rather than quietly dropped.
/// </summary>
public class EnumChoiceTests
{
    public enum Tone
    {
        Calm,
        [Description("upset")]
        Angry,
    }

    public enum Mood
    {
        [JsonStringEnumMemberName("furious")]
        Angry,
        Calm,
    }

    private static readonly Named<ChoiceAnswer<Tone>> ToneQuestion = Question.Named.Choice<Tone>("tone", "Tone?");

    private static readonly Named<ChoiceAnswer<Mood>> MoodQuestion = Question.Named.Choice<Mood>("mood", "Mood?");

    private static string Wire(Question question, string name = "q") => Question.Serialize(name, question).ToJsonString();

    /// <summary>Answers the stub with <paramref name="answers"/> under the <c>answers</c> key.</summary>
    private static async Task<SystemOneResponse> Respond(string answers, params INamedQuestion[] questions)
    {
        var body = $$"""
            { "model": "jev-latest", "usage": { "input_tokens": 1, "output_tokens": 1 }, "answers": {{answers}} }
            """;
        using var client = Clients.Create(new StubHandler((_, _) => Http.Json(200, body, ("x-typesafe-request-id", "req-9"))));
        return await client.SystemOneAsync("x", questions);
    }

    private const string AngryTone = """
        { "tone": { "type": "choice", "choice": "angry", "confidence": 0.7, "probabilities": { "calm": 0.3, "angry": 0.7 } } }
        """;

    [Fact]
    public void CriteriaAreTheEnumLabelsInDeclarationOrderWithTheirDescriptions()
    {
        Assert.Equal("tone", ToneQuestion.Name);
        var question = Assert.IsType<ChoiceQuestion<Tone>>(ToneQuestion.Question);
        Assert.Equal("choice", question.Type);
        Assert.Equal(new[] { "calm", "angry" }, question.Criteria.Keys);

        Assert.Equal(
            """{"type":"choice","instructions":"Tone?","criteria":{"calm":null,"angry":"upset"}}""",
            Wire(question));
    }

    /// <summary>
    /// The generic question is not registered with the source-generated context — an open generic can
    /// carry no discriminator — so it serializes through the plain <see cref="ChoiceQuestion"/> it projects
    /// onto, and the request body is byte-for-byte what the label-based builder produces.
    /// </summary>
    [Fact]
    public void TheWireJsonIsThatOfAPlainChoiceQuestion()
    {
        Assert.Equal(
            Wire(Question.Choice("Tone?", new Dictionary<string, Content> { ["calm"] = Content.Null, ["angry"] = "upset" })),
            Wire(ToneQuestion.Question));

        // Instructions are optional, as on every other builder.
        Assert.Equal("""{"type":"choice","criteria":{"calm":null,"angry":"upset"}}""", Wire(new ChoiceQuestion<Tone>()));
    }

    [Fact]
    public async Task TheRequestCarriesTheEnumCriteriaAndTheAnswerComesBackAsTheEnum()
    {
        var handler = new StubHandler((_, _) => Http.Json(200, $$"""
            { "model": "jev-latest", "usage": { "input_tokens": 1, "output_tokens": 1 }, "answers": {{AngryTone}} }
            """));
        using var client = Clients.Create(handler);

        var response = await client.SystemOneAsync("x", ToneQuestion);

        Assert.Equal(
            """{"type":"choice","instructions":"Tone?","criteria":{"calm":null,"angry":"upset"}}""",
            handler.Requests[0].Json!["questions"]!["tone"]!.ToJsonString());

        ChoiceAnswer<Tone> tone = response.Get(ToneQuestion);
        Assert.Equal(Tone.Angry, tone.Choice);
        Assert.Equal(0.7, tone.Confidence);
        Assert.Equal(0.3, tone.Probabilities[Tone.Calm]);
        Assert.Equal(0.7, tone.Probabilities[Tone.Angry]);
        Assert.Equal("choice", tone.Type);

        // Each read converts afresh, so the two answers are equal member by member rather than the same object.
        Assert.True(response.TryGet(ToneQuestion, out var same));
        Assert.Equal(tone.Choice, same.Choice);
        Assert.Equal(tone.Confidence, same.Confidence);
        Assert.Equal(tone.Probabilities, same.Probabilities);

        // The wire answer is still the string-labelled one; the enum view is built on top of it.
        Assert.Equal("angry", response.Choices["tone"].Choice);
    }

    [Fact]
    public async Task AJsonStringEnumMemberNameLabelRoundTrips()
    {
        Assert.Equal("""{"type":"choice","instructions":"Mood?","criteria":{"furious":null,"calm":null}}""", Wire(MoodQuestion.Question));

        var response = await Respond("""
            { "mood": { "type": "choice", "choice": "furious", "confidence": 0.9, "probabilities": { "furious": 0.9, "calm": 0.1 } } }
            """, MoodQuestion);

        var mood = response.Get(MoodQuestion);
        Assert.Equal(Mood.Angry, mood.Choice);
        Assert.Equal(0.9, mood.Probabilities[Mood.Angry]);
        Assert.Equal(0.1, mood.Probabilities[Mood.Calm]);
    }

    [Fact]
    public async Task AnUndeclaredChoiceLabelIsInvalidResponseData()
    {
        var response = await Respond("""
            { "tone": { "type": "choice", "choice": "furious", "confidence": 0.7, "probabilities": { "calm": 0.3, "furious": 0.7 } } }
            """, ToneQuestion);

        var error = Assert.Throws<TypeSafeApiResponseValidationException>(() => response.Get(ToneQuestion));
        Assert.Equal("answers.tone.choice", error.FieldPath);
        Assert.Equal("Invalid response data at 'answers.tone.choice'.", error.Detail);
        Assert.Equal(HttpStatusCode.OK, error.StatusCode);
        Assert.Equal("req-9", error.RequestId);
        Assert.Contains("furious", error.Body!.ToJsonString());
        Assert.Equal("POST https://api.typesafe.ai/v1/systemone", error.Endpoint);

        // The non-throwing form reports the same failure as a miss.
        Assert.False(response.TryGet(ToneQuestion, out var missing));
        Assert.Null(missing);
    }

    [Fact]
    public async Task AnUndeclaredProbabilityLabelIsInvalidResponseDataNamingTheLabel()
    {
        var response = await Respond("""
            { "tone": { "type": "choice", "choice": "calm", "confidence": 0.6, "probabilities": { "calm": 0.6, "furious": 0.4 } } }
            """, ToneQuestion);

        var error = Assert.Throws<TypeSafeApiResponseValidationException>(() => response.Get(ToneQuestion));
        Assert.Equal("answers.tone.probabilities.furious", error.FieldPath);
        Assert.False(response.TryGet(ToneQuestion, out _));
    }

    /// <summary>An answer of another type is still a mismatch, reported exactly as it always was.</summary>
    [Fact]
    public async Task AnAnswerOfAnotherTypeKeepsTheMismatchMessage()
    {
        var response = await Respond("""{ "tone": { "type": "noul", "noul": 0.5 } }""", ToneQuestion);

        var error = Assert.Throws<TypeSafeException>(() => response.Get(ToneQuestion));
        Assert.IsType<TypeSafeException>(error);
        Assert.Equal($"Answer \"tone\" is a NoulAnswer, not a {typeof(ChoiceAnswer<Tone>).Name}.", error.Message);
        Assert.False(response.TryGet(ToneQuestion, out _));

        var unmodelled = await Respond("""{ "tone": { "type": "rank", "rank": 2 } }""", ToneQuestion);
        Assert.Contains(
            "Answer \"tone\" has the unrecognized type \"rank\"",
            Assert.Throws<TypeSafeException>(() => unmodelled.Get(ToneQuestion)).Message);
    }

    /// <summary>A missing answer stays the plain lookup failure, not invalid response data.</summary>
    [Fact]
    public async Task AMissingAnswerIsStillANameFailure()
    {
        var response = await Respond(AngryTone, ToneQuestion);

        var error = Assert.Throws<TypeSafeException>(() => response.Get(Question.Named.Choice<Tone>("mood", "Mood?")));
        Assert.Equal("""No answer named "mood" in the response.""", error.Message);
        Assert.IsType<TypeSafeException>(error);
    }

    [Fact]
    public void AnEmptyNameAndADuplicateLabelAreRejected()
    {
        Assert.Contains("must not be empty", Assert.Throws<TypeSafeException>(() => Question.Named.Choice<Tone>(" ")).Message);
        Assert.Contains("'same'", Assert.Throws<TypeSafeException>(() => Question.Named.Choice<Duplicated>("x")).Message);
    }

    public enum Duplicated
    {
        [JsonStringEnumMemberName("same")]
        First,
        [JsonStringEnumMemberName("same")]
        Second,
    }
}
