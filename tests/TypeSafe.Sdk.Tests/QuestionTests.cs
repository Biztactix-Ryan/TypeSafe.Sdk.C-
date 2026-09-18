using System.Text.Json.Nodes;

namespace TypeSafe.Tests;

public class QuestionTests
{
    private static string Wire(Question question, string name = "q") => question.ToJson(name).ToJsonString();

    [Fact]
    public void NoulSerializesInstructionsAndOptionalCriteria()
    {
        Assert.Equal("""{"type":"noul"}""", Wire(Question.Noul()));
        Assert.Equal("""{"type":"noul","instructions":"Is it urgent?"}""", Wire(Question.Noul("Is it urgent?")));
        Assert.Equal(
            """{"type":"noul","instructions":"Is it urgent?","criteria":{"true":"needs action today","false":{"note":"can wait"}}}""",
            Wire(Question.Noul("Is it urgent?", whenTrue: "needs action today", whenFalse: new { note = "can wait" })));
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
        var question = Question.Choice("Tone?", new Dictionary<string, string?>
        {
            ["calm"] = "measured language",
            ["angry"] = null,
        });
        Assert.Equal(
            """{"type":"choice","instructions":"Tone?","criteria":{"calm":"measured language","angry":null}}""",
            Wire(question));

        var structured = Question.Choice(new { text = "Tone?" }, new Dictionary<string, object?>
        {
            ["calm"] = new[] { "quiet", "polite" },
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
            Wire(Question.Score("Urgency?", "low", "mid", null, new { label = "high" })));
        Assert.Equal(
            """{"type":"score","criteria":["a","b"]}""",
            Wire(Question.Score(null, new List<string> { "a", "b" })));
    }

    [Fact]
    public void ScoreWithoutCriteriaIsRejectedWithItsName()
    {
        var error = Assert.Throws<TypeSafeException>(() => Wire(Question.Score("Urgency?"), "urgency"));
        Assert.Contains("\"urgency\"", error.Message);
        Assert.Contains("at least one score", error.Message);
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
        var questions = new Questions
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
        var error = Assert.Throws<TypeSafeException>(() => Question.Normalize(new Questions { ["a"] = null! }));
        Assert.Contains("\"a\"", error.Message);
    }
}
