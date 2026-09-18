using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TypeSafe.Internal;

namespace TypeSafe.Tests;

/// <summary>
/// Covers the source-generated <see cref="TypeSafeJsonContext"/>: its options, and that the wire body
/// records decode the response fixtures the SDK is expected to read.
/// </summary>
public class JsonContextTests
{
    [Fact]
    public void ContextOptionsRespectNullabilityRequiredParametersAndOutOfOrderMetadata()
    {
        var options = TypeSafeJsonContext.Default.Options;

        Assert.True(options.AllowOutOfOrderMetadataProperties);
        Assert.True(options.RespectNullableAnnotations);
        Assert.True(options.RespectRequiredConstructorParameters);

        // The wire is snake_case, so the policy is too: input_tokens, release_date, request_id.
        Assert.Same(JsonNamingPolicy.SnakeCaseLower, options.PropertyNamingPolicy);
    }

    [Theory]
    // RespectRequiredConstructorParameters: "name" has no default, so an absent name is a read error.
    [InlineData("""{"description":"Latest model","release_date":"2026-09-01"}""")]
    // RespectNullableAnnotations: Name is a non-nullable string, so a JSON null is a read error too.
    [InlineData("""{"name":null,"description":"Latest model","release_date":"2026-09-01"}""")]
    public void AMissingOrNullNonNullableConstructorParameterIsARejectedRead(string json)
    {
        // The two options are not decoration: without them these reads would hand back a
        // ModelMetadata whose non-nullable Name is null.
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize(json, TypeSafeJsonContext.Default.ModelMetadata));
    }

    [Fact]
    public void TypeDiscriminatorAfterTheOtherFieldsStillPicksTheDerivedRecord()
    {
        var tone = Assert.IsType<ChoiceAnswer>(JsonSerializer.Deserialize(
            """{"choice":"frustrated","confidence":0.81,"probabilities":{"calm":0.05,"angry":0.14},"type":"choice"}""",
            TypeSafeJsonContext.Default.Answer));
        Assert.Equal("frustrated", tone.Choice);
        Assert.Equal(0.81, tone.Confidence);
        Assert.Equal(0.14, tone.Probabilities["angry"]);

        var billing = Assert.IsType<NoulAnswer>(JsonSerializer.Deserialize(
            """{"noul":0.93,"type":"noul"}""", TypeSafeJsonContext.Default.Answer));
        Assert.Equal(0.93, billing.Noul);

        var urgency = Assert.IsType<ScoreAnswer>(JsonSerializer.Deserialize(
            """{"score":2.4,"confidence":0.7,"legend":{},"probabilities":{},"type":"score"}""",
            TypeSafeJsonContext.Default.Answer));
        Assert.Equal(2.4, urgency.Score);
    }

    [Fact]
    public void ABodyWhoseAnswersPutTheTypeDiscriminatorLastStillPicksTheDerivedRecords()
    {
        // Property order is the server's to choose: a whole body whose nested answers end with the
        // discriminator has to decode exactly like Http.SystemOneBody, which leads with it.
        const string json = """
            {
              "model": "jev-latest",
              "usage": { "input_tokens": 120, "output_tokens": 8 },
              "answers": {
                "billing": { "noul": 0.93, "type": "noul" },
                "tone": {
                  "choice": "frustrated",
                  "confidence": 0.81,
                  "probabilities": { "calm": 0.05, "frustrated": 0.81, "angry": 0.14 },
                  "type": "choice"
                },
                "urgency": {
                  "score": 2.4,
                  "confidence": 0.7,
                  "legend": { "0": "can wait", "3": { "label": "right now" } },
                  "probabilities": { "0": 0.5, "3": 0.5 },
                  "type": "score"
                }
              }
            }
            """;

        var body = JsonSerializer.Deserialize(json, TypeSafeJsonContext.Default.SystemOneBody);

        Assert.NotNull(body);
        Assert.Equal(3, body.Answers.Count);
        Assert.Equal(0.93, Assert.IsType<NoulAnswer>(body.Answers["billing"]).Noul);

        var tone = Assert.IsType<ChoiceAnswer>(body.Answers["tone"]);
        Assert.Equal("frustrated", tone.Choice);
        Assert.Equal(0.14, tone.Probabilities["angry"]);

        var urgency = Assert.IsType<ScoreAnswer>(body.Answers["urgency"]);
        Assert.Equal(2.4, urgency.Score);
        Assert.Equal("right now", (string?)urgency.Legend[3]!["label"]);
    }

    [Fact]
    public void SystemOneBodyFixtureDeserializesThroughTheContext()
    {
        var body = JsonSerializer.Deserialize(Http.SystemOneBody, TypeSafeJsonContext.Default.SystemOneBody);

        Assert.NotNull(body);
        Assert.Equal("jev-latest", body.Model);
        Assert.Equal(120, body.Usage.InputTokens);
        Assert.Equal(8, body.Usage.OutputTokens);
        Assert.Null(body.RequestId);
        Assert.Equal(3, body.Answers.Count);

        var billing = Assert.IsType<NoulAnswer>(body.Answers["billing"]);
        Assert.Equal(0.93, billing.Noul);

        var tone = Assert.IsType<ChoiceAnswer>(body.Answers["tone"]);
        Assert.Equal("frustrated", tone.Choice);
        Assert.Equal(0.81, tone.Confidence);
        Assert.Equal(0.14, tone.Probabilities["angry"]);

        var urgency = Assert.IsType<ScoreAnswer>(body.Answers["urgency"]);
        Assert.Equal(2.4, urgency.Score);
        Assert.Equal(0.7, urgency.Confidence);
        Assert.Equal(new[] { 0, 1, 2, 3 }, urgency.Legend.Keys.OrderBy(k => k));
        Assert.Equal("today", (string?)urgency.Legend[2]);
        Assert.Equal("right now", (string?)urgency.Legend[3]!["label"]);
        Assert.Equal(0.5, urgency.Probabilities[3]);
    }

    [Fact]
    public void AnAnswerTypeTheSdkDoesNotModelDecodesWholeIntoExtensionData()
    {
        // FallBackToBaseType is a serialization-only setting (see the test below), so the answers map
        // is read entry by entry: an unmodelled discriminator must not fail the body, and nothing it
        // carried -- "type" included -- may be lost.
        const string json = """
            {"model":"m","usage":{},"answers":{
              "known":{"type":"noul","noul":0.5},
              "mystery":{"type":"rank","order":["a","b"]}
            }}
            """;

        var body = JsonSerializer.Deserialize(json, TypeSafeJsonContext.Default.SystemOneBody);

        Assert.NotNull(body);
        Assert.Equal(2, body.Answers.Count);
        Assert.Equal(0.5, Assert.IsType<NoulAnswer>(body.Answers["known"]).Noul);

        var mystery = body.Answers["mystery"];
        Assert.Equal(typeof(Answer), mystery.GetType());
        Assert.Equal("", mystery.Type);
        Assert.Equal("rank", mystery.AdditionalProperties!["type"].GetString());
        Assert.Equal(JsonValueKind.Array, mystery.AdditionalProperties["order"].ValueKind);

        // The discriminator is ordinary extension data on the way out too, so a bare answer written
        // back reproduces the payload it arrived as (FallBackToBaseType writes no discriminator of
        // its own for the base type).
        Assert.Equal("""{"type":"rank","order":["a","b"]}""",
            JsonSerializer.Serialize(mystery, TypeSafeJsonContext.Default.Answer));
    }

    /// <summary>
    /// The reason <see cref="AnswerMapConverter"/> exists, asserted rather than assumed:
    /// <see cref="JsonUnknownDerivedTypeHandling.FallBackToBaseType"/> is a write-side setting, so the
    /// polymorphic *reader* rejects a discriminator it does not know instead of falling back to
    /// <see cref="Answer"/>.
    /// </summary>
    [Fact]
    public void AnUnknownDiscriminatorReadStraightThroughTheContextIsARejectedRead()
    {
        var error = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(
            """{"type":"rank","order":["a","b"]}""", TypeSafeJsonContext.Default.Answer));

        // As of .NET 10: "Read unrecognized type discriminator id 'rank'. Path: $ | LineNumber: 0 |
        // BytePositionInLine: 23."
        Assert.Contains("unrecognized type discriminator", error.Message);
        Assert.Contains("rank", error.Message);
        Assert.Equal("$", error.Path);
    }

    [Theory]
    // A missing discriminator is not a forward-compatible answer, it is an unreadable one.
    [InlineData("""{"model":"m","usage":{},"answers":{"b":{"noul":0.1}}}""", "$.answers.b.type")]
    // A non-string discriminator counts as missing.
    [InlineData("""{"model":"m","usage":{},"answers":{"b":{"type":7}}}""", "$.answers.b.type")]
    // An answer that is not an object at all.
    [InlineData("""{"model":"m","usage":{},"answers":{"b":"nope"}}""", "$.answers.b")]
    // The map itself must be an object.
    [InlineData("""{"model":"m","usage":{},"answers":7}""", "$.answers")]
    // A modelled answer's own defects keep the path they would have had in a plain read.
    [InlineData("""{"model":"m","usage":{},"answers":{"b":{"type":"noul","noul":"high"}}}""", "$.answers.b.noul")]
    public void AnUnreadableAnswerKeepsItsJsonPath(string json, string expectedPath)
    {
        var error = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize(json, TypeSafeJsonContext.Default.SystemOneBody));

        Assert.Equal(expectedPath, error.Path);
    }

    [Fact]
    public void ALegendKeyThatIsNotAnIntegerIsARejectedRead()
    {
        // The int-key contract is real: a non-numeric rubric level is a read error, not a silent skip.
        const string json =
            """{"score":1.0,"confidence":0.5,"legend":{"high":"right now"},"probabilities":{"1":1.0},"type":"score"}""";

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize(json, TypeSafeJsonContext.Default.Answer));
    }

    [Fact]
    public void SystemOneBodyReadsRequestIdFromTheBodyWhenPresent()
    {
        const string json = """{"model":"m","usage":{},"answers":{},"request_id":"req-123"}""";

        var body = JsonSerializer.Deserialize(json, TypeSafeJsonContext.Default.SystemOneBody);

        Assert.NotNull(body);
        Assert.Equal("req-123", body.RequestId);
        Assert.Null(body.Usage.InputTokens);
        Assert.Null(body.Usage.OutputTokens);
        Assert.Empty(body.Answers);
    }

    [Fact]
    public void ModelListFixtureDeserializesThroughTheContext()
    {
        var list = JsonSerializer.Deserialize(Http.ModelsBody, TypeSafeJsonContext.Default.ModelList);

        Assert.NotNull(list);
        Assert.Equal(2, list.Models.Count);
        Assert.Equal("jev-latest", list.Models[0].Name);
        Assert.Equal("Latest model", list.Models[0].Description);
        Assert.Equal("2026-09-01", list.Models[0].ReleaseDate);
        Assert.Equal("jev-1", list.Models[1].Name);
        Assert.Equal("2026-01-01", list.Models[1].ReleaseDate);
    }

    [Fact]
    public void BodyRecordsRoundTripToTheSnakeCaseWireNames()
    {
        var json = JsonSerializer.Serialize(
            new SystemOneBody("m", new Usage(1, 2), new Dictionary<string, Answer> { ["b"] = new NoulAnswer(0.5) }, "req-1"),
            TypeSafeJsonContext.Default.SystemOneBody);

        Assert.Contains("\"input_tokens\":1", json);
        Assert.Contains("\"output_tokens\":2", json);
        Assert.Contains("\"request_id\":\"req-1\"", json);
        Assert.Contains("\"type\":\"noul\"", json);

        var models = JsonSerializer.Serialize(
            new ModelList([new ModelMetadata("jev-1", "First model", "2026-01-01")]),
            TypeSafeJsonContext.Default.ModelList);

        Assert.Contains("\"release_date\":\"2026-01-01\"", models);
    }

    [Fact]
    public void TheRequestEnvelopeIsWrittenVerbatimDespiteTheSnakeCaseNamingPolicy()
    {
        // A JsonObject's keys are data, not property names, so the context's snake_case policy must
        // leave the caller's ExtraBody keys and a RawQuestion's unmodelled fields exactly as given.
        var envelope = new JsonObject
        {
            ["state"] = "x",
            ["camelCaseKey"] = 1,
            ["questions"] = new JsonObject
            {
                ["tone"] = new JsonObject { ["type"] = "choice", ["future_field"] = 42 },
            },
        };

        Assert.Equal(
            """{"state":"x","camelCaseKey":1,"questions":{"tone":{"type":"choice","future_field":42}}}""",
            JsonSerializer.Serialize(envelope, TypeSafeJsonContext.Default.JsonObject));
    }
}
