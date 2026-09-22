using System.Text.Json;
using System.Text.Json.Nodes;
using TypeSafe.Internal;

namespace TypeSafe.Tests;

/// <summary>
/// The answers of a response keep the order the server wrote them in — they are decoded into an
/// <see cref="OrderedDictionary{TKey, TValue}"/>, not a hash map — and are reachable by position.
/// </summary>
public class AnswerOrderTests
{
    /// <summary>
    /// Five answers in a deliberately non-alphabetical order, two of each of two types, so both the whole
    /// map and the per-type views have an order a hash map would not reproduce by accident.
    /// </summary>
    private const string OutOfOrderBody = """
        {
          "model": "jev-latest",
          "usage": { "input_tokens": 1, "output_tokens": 2 },
          "answers": {
            "zeta": { "type": "noul", "noul": 0.1 },
            "alpha": { "type": "choice", "choice": "calm", "confidence": 0.5, "probabilities": { "calm": 0.5 } },
            "mid": { "type": "score", "score": 1.0, "confidence": 0.5, "legend": { "0": "low", "1": "high" }, "probabilities": { "0": 0.4, "1": 0.6 } },
            "beta": { "type": "noul", "noul": 0.2 },
            "aardvark": { "type": "choice", "choice": "angry", "confidence": 0.6, "probabilities": { "angry": 0.6 } }
          }
        }
        """;

    private static readonly string[] WireOrder = ["zeta", "alpha", "mid", "beta", "aardvark"];

    private static async Task<SystemOneResponse> Respond(string body = OutOfOrderBody)
    {
        using var client = Clients.Create(new StubHandler((_, _) => Http.Json(200, body)));
        return await client.SystemOneAsync("x", Clients.SampleQuestions());
    }

    /// <summary>
    /// The acceptance criterion: wire order out of <see cref="SystemOneResponse.Answers"/>, the same
    /// order out of the typed views, and index access by position.
    /// </summary>
    [Fact]
    public async Task AnswersEnumerateInWireOrderAndAreReachableByIndex()
    {
        var response = await Respond();

        Assert.Equal(WireOrder, response.Answers.Keys);
        Assert.Equal(WireOrder, response.Answers.Select(entry => entry.Key));
        Assert.IsType<OrderedDictionary<string, Answer>>(response.Answers, exactMatch: false);

        Assert.Equal(5, response.Count);
        Assert.Equal(response.Answers.Count, response.Count);
        Assert.Equal("zeta", response.GetAt(0).Key);
        Assert.Equal(0.1, Assert.IsType<NoulAnswer>(response.GetAt(0).Value).Noul);
        Assert.Equal("alpha", response.GetAt(1).Key);
        Assert.Equal("mid", response.GetAt(2).Key);
        Assert.Equal("aardvark", response.GetAt(4).Key);
        Assert.Same(response.Answers["aardvark"], response.GetAt(4).Value);

        // The per-type views are filtered in the same pass, so they keep wire order too.
        Assert.Equal(new[] { "zeta", "beta" }, response.Nouls.Keys);
        Assert.Equal(new[] { "alpha", "aardvark" }, response.Choices.Keys);
        Assert.Equal(new[] { "mid" }, response.Scores.Keys);
    }

    [Fact]
    public async Task GetAtRejectsAnIndexOutsideTheAnswers()
    {
        var response = await Respond();

        Assert.Throws<ArgumentOutOfRangeException>(() => response.GetAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => response.GetAt(5));
    }

    /// <summary>
    /// A response assembled in code from a plain dictionary still supports index access: the constructor
    /// copies such a map into an ordered one, in that map's own enumeration order.
    /// </summary>
    [Fact]
    public void AResponseBuiltFromAPlainDictionaryStillHasIndexAccess()
    {
        var answers = new Dictionary<string, Answer>(StringComparer.Ordinal)
        {
            ["second"] = new NoulAnswer(0.2),
            ["first"] = new NoulAnswer(0.1),
        };

        var response = new SystemOneResponse("m", new Usage(), answers);

        Assert.Equal(answers.Keys, response.Answers.Keys);
        Assert.Equal(answers.Keys.First(), response.GetAt(0).Key);
        Assert.Equal(2, response.Count);
    }

    /// <summary>The map the converter builds is handed out as-is, not copied into a second dictionary.</summary>
    [Fact]
    public void AnOrderedMapIsKeptRatherThanCopied()
    {
        var answers = new OrderedDictionary<string, Answer>(StringComparer.Ordinal) { ["one"] = new NoulAnswer(0.1) };

        Assert.Same(answers, new SystemOneResponse("m", new Usage(), answers).Answers);
    }

    /// <summary>
    /// The converter is the thing that fixes the order, so it also holds when the body is decoded straight
    /// through the source-generated context, and writing the body back out reproduces that order.
    /// </summary>
    [Fact]
    public void TheConverterDecodesAndReEncodesInWireOrder()
    {
        var body = JsonSerializer.Deserialize(OutOfOrderBody, TypeSafeJsonContext.Default.SystemOneBody);

        Assert.NotNull(body);
        Assert.Equal(WireOrder, body.Answers.Keys);

        var json = JsonSerializer.Serialize(body, TypeSafeJsonContext.Default.SystemOneBody);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            WireOrder,
            document.RootElement.GetProperty("answers").EnumerateObject().Select(property => property.Name));
    }
}

/// <summary>
/// <c>Nouls</c>, <c>Choices</c> and <c>Scores</c> are extension members on any map of answers, so a map a
/// caller assembled has the same typed views a response does.
/// </summary>
public class AnswerDictionaryExtensionTests
{
    private static readonly NoulAnswer Billing = new(0.93);
    private static readonly ChoiceAnswer Tone = new("frustrated", 0.81, new Dictionary<string, double> { ["frustrated"] = 0.81 });
    private static readonly ScoreAnswer Urgency = new(2.4, 0.7, new Dictionary<int, JsonNode?>(), new Dictionary<int, double>());
    private static readonly Answer Mystery = new();

    /// <summary>A plain dictionary — not a response, not an ordered map — typed by the extension members.</summary>
    [Fact]
    public void TheTypedViewsWorkOnAPlainDictionary()
    {
        IReadOnlyDictionary<string, Answer> answers = new Dictionary<string, Answer>(StringComparer.Ordinal)
        {
            ["billing"] = Billing,
            ["tone"] = Tone,
            ["urgency"] = Urgency,
            ["mystery"] = Mystery,
        };

        Assert.Same(Billing, Assert.Single(answers.Nouls).Value);
        Assert.Same(Tone, Assert.Single(answers.Choices).Value);
        Assert.Same(Urgency, Assert.Single(answers.Scores).Value);

        // A bare Answer — an answer type this SDK version does not model — reaches none of the views.
        Assert.DoesNotContain("mystery", answers.Nouls.Keys);
        Assert.DoesNotContain("mystery", answers.Choices.Keys);
        Assert.DoesNotContain("mystery", answers.Scores.Keys);
    }

    /// <summary>Filtering keeps the order of the map it filtered, whatever that map is.</summary>
    [Fact]
    public void TheTypedViewsKeepTheOrderOfTheMapTheyFilter()
    {
        IReadOnlyDictionary<string, Answer> answers = new OrderedDictionary<string, Answer>(StringComparer.Ordinal)
        {
            ["zeta"] = Billing,
            ["tone"] = Tone,
            ["alpha"] = new NoulAnswer(0.1),
        };

        Assert.Equal(new[] { "zeta", "alpha" }, answers.Nouls.Keys);
    }

    /// <summary>
    /// <see cref="SystemOneResponse"/> declares the same three names as instance properties that forward
    /// here, so an instance member wins the lookup and the result stays cached rather than rebuilt per call.
    /// </summary>
    [Fact]
    public async Task TheResponsePropertiesStillWinAndStayCached()
    {
        using var client = Clients.Create(new StubHandler((_, _) => Http.Json(200, Http.SystemOneBody)));
        var response = await client.SystemOneAsync("x", Clients.SampleQuestions());

        Assert.Same(response.Nouls, response.Nouls);
        Assert.Same(response.Choices, response.Choices);
        Assert.Same(response.Scores, response.Scores);

        // The extension member builds a fresh view each time, which is how the cached one differs.
        IReadOnlyDictionary<string, Answer> answers = response.Answers;
        Assert.NotSame(answers.Nouls, answers.Nouls);
        Assert.Equal(response.Nouls.Keys, answers.Nouls.Keys);
    }
}
