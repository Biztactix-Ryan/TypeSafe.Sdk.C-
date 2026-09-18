namespace TypeSafe.Tests;

/// <summary>
/// The name-first builders behind <c>Question.Named</c>, the <see cref="Named{TAnswer}"/> pair they
/// return, and the guarantee that adding them did not move a single positional call.
/// </summary>
public class NamedQuestionTests
{
    private static string Wire(Question question, string name = "q") => Question.Serialize(name, question).ToJsonString();

    [Fact]
    public void NoulNamesTheQuestionAndKeepsItsAnswerType()
    {
        Named<NoulAnswer> named = Question.Named.Noul("billing", "Is this a billing issue?");
        Assert.Equal("billing", named.Name);
        var question = Assert.IsType<NoulQuestion>(named.Question);
        Assert.Equal("""{"type":"noul","instructions":"Is this a billing issue?"}""", Wire(question));
    }

    [Fact]
    public void NoulCarriesBothOutcomeDescriptions()
    {
        var named = Question.Named.Noul("urgency", "Is it urgent?", whenTrue: "needs action today", whenFalse: "can wait");
        Assert.Equal(
            """{"type":"noul","instructions":"Is it urgent?","criteria":{"true":"needs action today","false":"can wait"}}""",
            Wire(named.Question));
    }

    [Fact]
    public void ChoiceFromLabelsNamesTheQuestion()
    {
        Named<ChoiceAnswer> named = Question.Named.Choice("tone", "Tone?", "calm", "angry");
        Assert.Equal("tone", named.Name);
        Assert.IsType<ChoiceQuestion>(named.Question);
        Assert.Equal(
            """{"type":"choice","instructions":"Tone?","criteria":{"calm":null,"angry":null}}""",
            Wire(named.Question));
    }

    [Fact]
    public void ChoiceFromDictionaryNamesTheQuestion()
    {
        Named<ChoiceAnswer> named = Question.Named.Choice("tone", "Tone?", new Dictionary<string, Content>
        {
            ["calm"] = "measured language",
            ["angry"] = Content.Null,
        });
        Assert.Equal("tone", named.Name);
        Assert.Equal(
            """{"type":"choice","instructions":"Tone?","criteria":{"calm":"measured language","angry":null}}""",
            Wire(named.Question));
    }

    [Fact]
    public void ScoreNamesTheQuestionAndAcceptsACollectionExpression()
    {
        Named<ScoreAnswer> named = Question.Named.Score("urgency", "How urgent?", ["can wait", "today"]);
        Assert.Equal("urgency", named.Name);
        Assert.IsType<ScoreQuestion>(named.Question);
        Assert.Equal(
            """{"type":"score","instructions":"How urgent?","criteria":["can wait","today"]}""",
            Wire(named.Question));
    }

    [Fact]
    public void NamedQuestionsAreReachableThroughTheNonGenericInterface()
    {
        INamedQuestion[] questions =
        [
            Question.Named.Noul("billing", "Is this a billing issue?"),
            Question.Named.Choice("tone", "Tone?", "calm", "angry"),
            Question.Named.Score("urgency", "How urgent?", ["can wait", "today"]),
        ];

        Assert.Equal(new[] { "billing", "tone", "urgency" }, questions.Select(q => q.Name));
        Assert.Collection(
            questions.Select(q => q.Question),
            question => Assert.IsType<NoulQuestion>(question),
            question => Assert.IsType<ChoiceQuestion>(question),
            question => Assert.IsType<ScoreQuestion>(question));

        // The explicit implementation hands back the very same question the typed property holds.
        var named = Question.Named.Noul("billing", "Is this a billing issue?");
        Assert.Same(named.Question, ((INamedQuestion)named).Question);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\n")]
    public void AnEmptyNameIsRejected(string name)
    {
        Assert.Contains("must not be empty", Assert.Throws<TypeSafeException>(() => Question.Named.Noul(name)).Message);
        Assert.Throws<TypeSafeException>(() => Question.Named.Choice(name, "Tone?", "calm"));
        Assert.Throws<TypeSafeException>(() => Question.Named.Choice(name, "Tone?", new Dictionary<string, Content>()));
        Assert.Throws<TypeSafeException>(() => Question.Named.Score(name, "How urgent?", ["can wait"]));
        Assert.Throws<TypeSafeException>(() => new Named<NoulAnswer>(name, Question.Noul()));
    }

    [Fact]
    public void BothFamiliesProduceTheSameWireJson()
    {
        Assert.Equal(
            Wire(Question.Noul("Is it urgent?", whenTrue: "today")),
            Wire(Question.Named.Noul("urgency", "Is it urgent?", whenTrue: "today").Question));
        Assert.Equal(
            Wire(Question.Choice("Tone?", "calm", "angry")),
            Wire(Question.Named.Choice("tone", "Tone?", "calm", "angry").Question));
        Assert.Equal(
            Wire(Question.Choice("Tone?", new Dictionary<string, Content> { ["calm"] = "measured language" })),
            Wire(Question.Named.Choice("tone", "Tone?", new Dictionary<string, Content> { ["calm"] = "measured language" }).Question));
        Assert.Equal(
            Wire(Question.Score("How urgent?", ["can wait", "today"])),
            Wire(Question.Named.Score("urgency", "How urgent?", ["can wait", "today"]).Question));
    }

    /// <summary>
    /// The reason the name-first builders sit on <c>Question.Named</c>: a lone string argument still binds
    /// to <c>Content instructions</c> on the positional builders, because nothing overloads them. The typed
    /// locals are the assertion — they would not compile if a call had re-bound to a name-first builder.
    /// </summary>
    [Fact]
    public void ALoneStringStillMeansInstructionsOnThePositionalBuilders()
    {
        NoulQuestion noul = Question.Noul("Is it urgent?");
        ChoiceQuestion choice = Question.Choice("Tone?", "calm", "angry");
        ScoreQuestion score = Question.Score("How urgent?", ["can wait", "today"]);

        Assert.Equal("""{"type":"noul","instructions":"Is it urgent?"}""", Wire(noul));
        Assert.Equal("""{"type":"choice","instructions":"Tone?","criteria":{"calm":null,"angry":null}}""", Wire(choice));
        Assert.Equal("""{"type":"score","instructions":"How urgent?","criteria":["can wait","today"]}""", Wire(score));
    }

    [Fact]
    public void ANamedQuestionIsAValueAndCanBeCopiedWithANewName()
    {
        var question = Question.Score("How urgent?", ["can wait", "today"]);
        var named = new Named<ScoreAnswer>("urgency", question);
        Assert.Equal(named, new Named<ScoreAnswer>("urgency", question));

        var renamed = named with { Name = "priority" };
        Assert.Equal("priority", renamed.Name);
        Assert.Same(named.Question, renamed.Question);
        Assert.Throws<TypeSafeException>(() => named with { Name = "  " });
    }

    [Fact]
    public void ARequestKeysNamedQuestionsByTheirNamesInOrder()
    {
        var billing = Question.Named.Noul("billing", "Is this a billing issue?");
        var tone = Question.Named.Choice("tone", "Tone?", "calm", "angry");

        var request = new SystemOneRequest("x", new INamedQuestion[] { tone, billing }) { Model = "m" };

        Assert.Equal(new[] { "tone", "billing" }, request.Questions.Select(q => q.Key));
        Assert.Same(tone.Question, request.Questions.First().Value);
        Assert.Equal("m", request.Model);
        Assert.Equal(
            """{"tone":{"type":"choice","instructions":"Tone?","criteria":{"calm":null,"angry":null}},"billing":{"type":"noul","instructions":"Is this a billing issue?"}}""",
            Question.Normalize(request.Questions).ToJsonString());
    }

    [Fact]
    public void ARequestRejectsARepeatedNameAndANullEntry()
    {
        var tone = Question.Named.Choice("tone", "Tone?", "calm", "angry");

        Assert.Equal(
            """Question name "tone" is used more than once.""",
            Assert.Throws<TypeSafeException>(
                () => new SystemOneRequest("x", new INamedQuestion[] { tone, Question.Named.Noul("tone") })).Message);
        Assert.Throws<TypeSafeException>(() => new SystemOneRequest("x", new INamedQuestion[] { tone, null! }));
        Assert.Throws<ArgumentNullException>(() => new SystemOneRequest("x", (IEnumerable<INamedQuestion>)null!));

        // An empty set is left to the shared validation, with the message the dictionary form produces.
        var empty = new SystemOneRequest("x", Array.Empty<INamedQuestion>());
        Assert.Equal(
            "At least one question is required.",
            Assert.Throws<TypeSafeException>(() => Question.Normalize(empty.Questions)).Message);
    }
}
