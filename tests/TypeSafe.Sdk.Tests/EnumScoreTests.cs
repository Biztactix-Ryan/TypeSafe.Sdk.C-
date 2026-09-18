using System.ComponentModel;
using System.Net;

namespace TypeSafe.Tests;

/// <summary>
/// Enum-typed score questions: the enum is the rubric, so it must number its levels from zero, the wire
/// shape is a plain score question built from the members' descriptions, and the answer comes back keyed
/// by the enum with a <c>Nearest</c> level, with a score outside the rubric reported as invalid response
/// data rather than quietly dropped.
/// </summary>
public class EnumScoreTests
{
    public enum Urgency
    {
        [Description("can wait")]
        Low = 0,
        [Description("today")]
        Medium = 1,
        [Description("right now")]
        High = 2,
    }

    /// <summary>A rubric whose middle level alone is described; the others fall back to their wire label.</summary>
    public enum Steps
    {
        Low,
        [Description("half way")]
        Half,
        VeryHigh,
    }

    /// <summary>Levels numbered from one: an ordered rubric, but not one a score can index.</summary>
    public enum Gapped
    {
        First = 1,
        Second = 2,
        Third = 3,
    }

    /// <summary>Starts at zero, but skips a level.</summary>
    public enum Sparse
    {
        Bottom = 0,
        Top = 2,
    }

    /// <summary>No levels at all, so no score could ever name one.</summary>
    public enum Unnumbered
    {
    }

    private static readonly Named<ScoreAnswer<Urgency>> UrgencyQuestion =
        Question.Named.Score<Urgency>("urgency", "How urgent?");

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

    /// <summary>The urgency answer with <paramref name="score"/> spliced in exactly as written.</summary>
    private static string Scored(string score) => $$"""
        {
          "urgency": {
            "type": "score",
            "score": {{score}},
            "confidence": 0.8,
            "legend": { "0": "can wait", "1": "today", "2": { "label": "right now" } },
            "probabilities": { "0": 0.1, "1": 0.5, "2": 0.4 }
          }
        }
        """;

    private const string NotARubric = " must have contiguous values starting at 0 to be used as a score rubric.";

    [Fact]
    public void CriteriaAreTheMembersDescriptionsInValueOrder()
    {
        Assert.Equal("urgency", UrgencyQuestion.Name);
        var question = Assert.IsType<ScoreQuestion<Urgency>>(UrgencyQuestion.Question);
        Assert.Equal("score", question.Type);
        Assert.Equal(
            new[] { "can wait", "today", "right now" },
            question.Criteria.Select(criterion => criterion!.GetValue<string>()));

        Assert.Equal(
            """{"type":"score","instructions":"How urgent?","criteria":["can wait","today","right now"]}""",
            Wire(question));
    }

    /// <summary>
    /// The generic question is not registered with the source-generated context — an open generic can
    /// carry no discriminator — so it serializes through the plain <see cref="ScoreQuestion"/> it projects
    /// onto, and the request body is byte-for-byte what the positional builder produces.
    /// </summary>
    [Fact]
    public void TheWireJsonIsThatOfAPlainScoreQuestion()
    {
        Assert.Equal(
            Wire(Question.Score("How urgent?", "can wait", "today", "right now")),
            Wire(UrgencyQuestion.Question));

        // An undescribed level falls back to its wire label, and instructions stay optional.
        Assert.Equal("""{"type":"score","criteria":["low","half way","very_high"]}""", Wire(new ScoreQuestion<Steps>()));
    }

    [Fact]
    public void AnEnumThatIsNotNumberedFromZeroIsRejected()
    {
        Assert.Equal(
            "Enum Gapped" + NotARubric,
            Assert.Throws<TypeSafeException>(() => Question.Named.Score<Gapped>("urgency")).Message);
        Assert.Equal(
            "Enum Sparse" + NotARubric,
            Assert.Throws<TypeSafeException>(() => Question.Named.Score<Sparse>("urgency")).Message);
        Assert.Equal(
            "Enum Unnumbered" + NotARubric,
            Assert.Throws<TypeSafeException>(() => Question.Named.Score<Unnumbered>("urgency")).Message);

        // The name is validated as it is on every other name-first builder.
        Assert.Contains("must not be empty", Assert.Throws<TypeSafeException>(() => Question.Named.Score<Urgency>(" ")).Message);
    }

    [Fact]
    public async Task TheRequestCarriesTheEnumRubricAndTheAnswerComesBackKeyedByTheEnum()
    {
        var handler = new StubHandler((_, _) => Http.Json(200, $$"""
            { "model": "jev-latest", "usage": { "input_tokens": 1, "output_tokens": 1 }, "answers": {{Scored("1.4")}} }
            """));
        using var client = Clients.Create(handler);

        var response = await client.SystemOneAsync("x", UrgencyQuestion);

        Assert.Equal(
            """{"type":"score","instructions":"How urgent?","criteria":["can wait","today","right now"]}""",
            handler.Requests[0].Json!["questions"]!["urgency"]!.ToJsonString());

        ScoreAnswer<Urgency> urgency = response.Get(UrgencyQuestion);
        Assert.Equal(1.4, urgency.Score);
        Assert.Equal(0.8, urgency.Confidence);
        Assert.Equal("can wait", urgency.Legend[Urgency.Low]!.GetValue<string>());
        Assert.Equal("""{"label":"right now"}""", urgency.Legend[Urgency.High]!.ToJsonString());
        Assert.Equal(0.5, urgency.Probabilities[Urgency.Medium]);
        Assert.Equal(0.4, urgency.Probabilities[Urgency.High]);
        Assert.Equal(Urgency.Medium, urgency.Nearest);
        Assert.Equal("score", urgency.Type);

        // Each read converts afresh, so the two answers are equal member by member rather than the same object.
        Assert.True(response.TryGet(UrgencyQuestion, out var same));
        Assert.Equal(urgency.Score, same.Score);
        Assert.Equal(urgency.Probabilities, same.Probabilities);
        Assert.Equal(urgency.Nearest, same.Nearest);

        // The wire answer is still the integer-keyed one; the enum view is built on top of it.
        Assert.Equal("can wait", response.Scores["urgency"].Legend[0]!.GetValue<string>());
        Assert.Equal(0.5, response.Scores["urgency"].Probabilities[1]);
    }

    /// <summary>
    /// The expected score falls between the rubric levels, so <c>Nearest</c> rounds it half away from zero
    /// and clamps it: a score above the top level is the top level, a negative one the bottom.
    /// </summary>
    [Theory]
    [InlineData("1.4", Urgency.Medium)]
    [InlineData("1.5", Urgency.High)]
    [InlineData("0.5", Urgency.Medium)]
    [InlineData("7.0", Urgency.High)]
    [InlineData("-1", Urgency.Low)]
    [InlineData("0", Urgency.Low)]
    public async Task NearestRoundsHalfAwayFromZeroAndClampsToTheRubric(string score, Urgency expected)
    {
        var response = await Respond(Scored(score), UrgencyQuestion);

        Assert.Equal(expected, response.Get(UrgencyQuestion).Nearest);
    }

    [Fact]
    public async Task AScoreOutsideTheRubricIsInvalidResponseData()
    {
        var response = await Respond("""
            {
              "urgency": {
                "type": "score",
                "score": 1.0,
                "confidence": 0.8,
                "legend": { "0": "can wait", "1": "today", "2": "right now", "3": "yesterday" },
                "probabilities": { "0": 0.1, "1": 0.5, "2": 0.4 }
              }
            }
            """, UrgencyQuestion);

        var error = Assert.Throws<TypeSafeApiResponseValidationException>(() => response.Get(UrgencyQuestion));
        Assert.Equal("answers.urgency.legend.3", error.FieldPath);
        Assert.Equal("Invalid response data at 'answers.urgency.legend.3'.", error.Detail);
        Assert.Equal(HttpStatusCode.OK, error.StatusCode);
        Assert.Equal("req-9", error.RequestId);
        Assert.Contains("yesterday", error.Body!.ToJsonString());
        Assert.Equal("POST https://api.typesafe.ai/v1/systemone", error.Endpoint);

        // The non-throwing form reports the same failure as a miss.
        Assert.False(response.TryGet(UrgencyQuestion, out var missing));
        Assert.Null(missing);
    }

    [Fact]
    public async Task AProbabilityOutsideTheRubricIsInvalidResponseDataNamingTheScore()
    {
        var response = await Respond("""
            {
              "urgency": {
                "type": "score",
                "score": 1.0,
                "confidence": 0.8,
                "legend": { "0": "can wait", "1": "today", "2": "right now" },
                "probabilities": { "0": 0.1, "1": 0.5, "2": 0.3, "4": 0.1 }
              }
            }
            """, UrgencyQuestion);

        var error = Assert.Throws<TypeSafeApiResponseValidationException>(() => response.Get(UrgencyQuestion));
        Assert.Equal("answers.urgency.probabilities.4", error.FieldPath);
        Assert.False(response.TryGet(UrgencyQuestion, out _));
    }

    /// <summary>An answer of another type is still a mismatch, reported exactly as it always was.</summary>
    [Fact]
    public async Task AnAnswerOfAnotherTypeKeepsTheMismatchMessage()
    {
        var response = await Respond("""{ "urgency": { "type": "noul", "noul": 0.5 } }""", UrgencyQuestion);

        var error = Assert.Throws<TypeSafeException>(() => response.Get(UrgencyQuestion));
        Assert.IsType<TypeSafeException>(error);
        Assert.Equal($"Answer \"urgency\" is a NoulAnswer, not a {typeof(ScoreAnswer<Urgency>).Name}.", error.Message);
        Assert.False(response.TryGet(UrgencyQuestion, out _));
    }

    /// <summary>The rubric need not be an enum: the positional builder still answers with integer keys.</summary>
    [Fact]
    public async Task TheUntypedScorePathStillYieldsIntegerKeys()
    {
        var untyped = Question.Named.Score("urgency", "How urgent?", "can wait", "today", "right now");

        var response = await Respond(Scored("1.4"), untyped);

        ScoreAnswer urgency = response.Get(untyped);
        Assert.IsType<ScoreAnswer>(urgency);
        Assert.Equal(1.4, urgency.Score);
        Assert.Equal("can wait", urgency.Legend[0]!.GetValue<string>());
        Assert.Equal(0.5, urgency.Probabilities[1]);
        Assert.Same(urgency, response.Scores["urgency"]);
    }
}
