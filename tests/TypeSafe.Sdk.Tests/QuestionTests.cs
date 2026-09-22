using System.Text.Json.Nodes;

namespace TypeSafe.Tests;

public class QuestionTests
{
    private static string Wire(Question question, string name = "q") => Question.Serialize(name, question).ToJsonString();

    [Fact]
    public void NoulSerializesInstructionsAndOptionalCriteria()
    {
        Assert.Equal("""{"type":"noul"}""", Wire(Question.Noul()));
        Assert.Equal("""{"type":"noul","instructions":"Is it urgent?"}""", Wire(Question.Noul("Is it urgent?")));
        Assert.Equal(
            """{"type":"noul","instructions":"Is it urgent?","criteria":{"true":"needs action today","false":{"note":"can wait"}}}""",
            Wire(Question.Noul("Is it urgent?", whenTrue: "needs action today", whenFalse: Content.From(new { note = "can wait" }))));
        Assert.Equal("""{"type":"noul","criteria":{"false":"nope"}}""", Wire(Question.Noul(whenFalse: "nope")));
    }

    [Fact]
    public void ChoiceFromLabelsHasNullDescriptions()
    {
        Assert.Equal(
            """{"type":"choice","instructions":"Tone?","criteria":{"calm":null,"angry":null}}""",
            Wire(Question.Choice("Tone?", "calm", "angry")));
    }

    [Fact]
    public void ChoiceFromDictionaryKeepsDescriptions()
    {
        var question = Question.Choice("Tone?", new Dictionary<string, Content>
        {
            ["calm"] = "measured language",
            ["angry"] = Content.Null,
        });
        Assert.Equal(
            """{"type":"choice","instructions":"Tone?","criteria":{"calm":"measured language","angry":null}}""",
            Wire(question));

        var structured = Question.Choice(Content.From(new { text = "Tone?" }), new Dictionary<string, Content>
        {
            ["calm"] = Content.From(new[] { "quiet", "polite" }),
        });
        Assert.Equal(
            """{"type":"choice","instructions":{"text":"Tone?"},"criteria":{"calm":["quiet","polite"]}}""",
            Wire(structured));
    }

    [Fact]
    public void ScoreSerializesOrderedCriteria()
    {
        Assert.Equal(
            """{"type":"score","instructions":"Urgency?","criteria":["low","mid",null,{"label":"high"}]}""",
            Wire(Question.Score("Urgency?", "low", "mid", Content.Null, Content.From(new { label = "high" }))));
        Assert.Equal(
            """{"type":"score","criteria":["a","b"]}""",
            Wire(Question.Score(Content.Null, ["a", "b"])));
    }

    [Fact]
    public void ScoreAcceptsACollectionExpressionOfStringCriteria()
    {
        Assert.Equal(
            """{"type":"score","instructions":"How urgent?","criteria":["can wait","today"]}""",
            Wire(Question.Score("How urgent?", ["can wait", "today"])));
    }

    [Fact]
    public void ScoreWithoutCriteriaIsRejectedWithItsName()
    {
        var error = Assert.Throws<TypeSafeException>(() => Wire(Question.Score("Urgency?"), "urgency"));
        Assert.Contains("\"urgency\"", error.Message);
        Assert.Contains("at least one score", error.Message);
    }

    [Fact]
    public void BuildersAcceptAnExplicitArrayForTheirSpanParameter()
    {
        // An array converts to ReadOnlySpan<T>, so a caller holding one keeps compiling and serializing
        // exactly as it did when these parameters were params T[].
        string[] labels = ["calm", "angry"];
        Content[] criteria = ["can wait", "today"];

        Assert.Equal(
            """{"type":"choice","instructions":"Tone?","criteria":{"calm":null,"angry":null}}""",
            Wire(Question.Choice("Tone?", labels)));
        Assert.Equal(
            """{"type":"score","instructions":"How urgent?","criteria":["can wait","today"]}""",
            Wire(Question.Score("How urgent?", criteria)));
        Assert.Equal(
            Wire(Question.Choice("Tone?", labels)),
            Wire(Question.Named.Choice("tone", "Tone?", labels).Question));
        Assert.Equal(
            Wire(Question.Score("How urgent?", criteria)),
            Wire(Question.Named.Score("urgency", "How urgent?", criteria).Question));
    }

    [Fact]
    public void BuildersAcceptAnEmptySpanAndKeepTheirValidation()
    {
        // Zero labels reach the wire as an empty criteria object, exactly as zero params did.
        Assert.Equal(
            """{"type":"choice","instructions":"Tone?","criteria":{}}""",
            Wire(Question.Choice("Tone?")));
        Assert.Equal(
            """{"type":"choice","instructions":"Tone?","criteria":{}}""",
            Wire(Question.Named.Choice("tone", "Tone?").Question, "tone"));

        // Zero criteria still fail score validation with the same message, from either builder.
        foreach (var empty in new Func<Question>[]
        {
            static () => Question.Score("Urgency?"),
            static () => Question.Score("Urgency?", []),
            static () => Question.Named.Score("urgency", "Urgency?").Question,
            static () => Question.Named.Score("urgency", "Urgency?", System.Array.Empty<Content>()).Question,
        })
        {
            var error = Assert.Throws<TypeSafeException>(() => Wire(empty(), "urgency"));
            Assert.Contains("\"urgency\"", error.Message);
            Assert.Contains("at least one score", error.Message);
        }
    }

    [Fact]
    public void RawQuestionsForwardEveryField()
    {
        var raw = Question.FromJson(new JsonObject
        {
            ["type"] = "choice",
            ["instructions"] = "Tone?",
            ["criteria"] = new JsonObject { ["calm"] = null },
            ["future_field"] = 42,
        });
        Assert.Equal("choice", raw.Type);
        Assert.Equal("Tone?", (string?)raw.Instructions);
        Assert.Equal("""{"type":"choice","instructions":"Tone?","criteria":{"calm":null},"future_field":42}""", Wire(raw));
    }

    [Fact]
    public void RawQuestionsAreValidated()
    {
        Assert.Contains("nonempty string \"type\"",
            Assert.Throws<TypeSafeException>(() => Wire(Question.FromJson(new JsonObject { ["instructions"] = "x" }))).Message);
        Assert.Contains("nonempty string \"type\"",
            Assert.Throws<TypeSafeException>(() => Wire(Question.FromJson(new JsonObject { ["type"] = "" }))).Message);
        Assert.Contains("requires \"criteria\"",
            Assert.Throws<TypeSafeException>(() => Wire(Question.FromJson(new JsonObject { ["type"] = "choice" }))).Message);
        Assert.Contains("not a list",
            Assert.Throws<TypeSafeException>(() => Wire(Question.FromJson(new JsonObject { ["type"] = "score", ["criteria"] = new JsonObject() }))).Message);
        Assert.Contains("at least one score",
            Assert.Throws<TypeSafeException>(() => Wire(Question.FromJson(new JsonObject { ["type"] = "score", ["criteria"] = new JsonArray() }))).Message);
        // Unknown types with a nonempty string are forwarded for forward compatibility.
        Assert.Equal("""{"type":"future"}""", Wire(Question.FromJson(new JsonObject { ["type"] = "future" })));
    }

    [Fact]
    public void JsonNodesCanBeReusedAcrossQuestions()
    {
        var shared = new JsonObject { ["text"] = "shared instructions" };
        var questions = new Dictionary<string, Question>
        {
            ["a"] = Question.Noul(shared),
            ["b"] = Question.Choice(shared, "x"),
        };
        var normalized = Question.Normalize(questions);
        Assert.Equal("shared instructions", (string?)normalized["a"]!["instructions"]!["text"]);
        Assert.Equal("shared instructions", (string?)normalized["b"]!["instructions"]!["text"]);
        // Serializing twice must not fail because nodes were re-parented.
        Question.Normalize(questions);
    }

    [Fact]
    public void NullQuestionIsRejected()
    {
        var error = Assert.Throws<TypeSafeException>(() => Question.Normalize(new Dictionary<string, Question> { ["a"] = null! }));
        Assert.Contains("\"a\"", error.Message);
    }

    [Fact]
    public void BuildersReturnQuestionsTypedByTheirAnswer()
    {
        // Each builder result assigns to Question<TAnswer> for the answer type it produces.
        Question<NoulAnswer> noul = Question.Noul("Is it urgent?");
        Question<ChoiceAnswer> choice = Question.Choice("Tone?", "calm", "angry");
        Question<ScoreAnswer> score = Question.Score("How clear?", "unclear", "clear");

        Assert.IsType<NoulQuestion>(noul);
        Assert.IsType<ChoiceQuestion>(choice);
        Assert.IsType<ScoreQuestion>(score);

        // The generic intermediate adds nothing to the wire shape.
        Assert.Equal("""{"type":"noul","instructions":"Is it urgent?"}""", Wire(noul));

        // RawQuestion stays on the non-generic base: its answer type is not known at compile time.
        Question raw = Question.FromJson(new JsonObject { ["type"] = "noul" });
        Assert.IsType<RawQuestion>(raw);
        Assert.False(raw is Question<NoulAnswer>);
    }
}
