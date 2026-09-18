using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace TypeSafe.Tests;

public class ResponseTests
{
    [Fact]
    public async Task SystemOneResponseIsDecodedIntoTypedAnswers()
    {
        var handler = new StubHandler((_, _) => Http.Json(200, Http.SystemOneBody, ("x-typesafe-request-id", "req-123")));
        using var client = Clients.Create(handler);

        var response = await client.SystemOneAsync("x", Clients.SampleQuestions());

        Assert.Equal("jev-latest", response.Model);
        Assert.Equal(120, response.Usage.InputTokens);
        Assert.Equal(8, response.Usage.OutputTokens);
        Assert.Equal(3, response.Answers.Count);

        var billing = response.Nouls["billing"];
        Assert.Equal(0.93, billing.Noul);
        Assert.Equal("noul", billing.Type);

        var tone = response.Choices["tone"];
        Assert.Equal("frustrated", tone.Choice);
        Assert.Equal(0.81, tone.Confidence);
        Assert.Equal(0.14, tone.Probabilities["angry"]);

        var urgency = response.Scores["urgency"];
        Assert.Equal(2.4, urgency.Score);
        Assert.Equal(0.7, urgency.Confidence);
        Assert.Equal(new[] { 0, 1, 2, 3 }, urgency.Legend.Keys.OrderBy(k => k));
        Assert.Equal("today", (string?)urgency.Legend[2]);
        Assert.Equal("right now", (string?)urgency.Legend[3]!["label"]);
        Assert.Equal(0.5, urgency.Probabilities[3]);

        Assert.Same(tone, response.Answers["tone"]);
        Assert.Single(response.Nouls);
        Assert.Single(response.Choices);
        Assert.Single(response.Scores);

        Assert.Equal("req-123", response.RequestId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("req-123", response.Headers["X-TypeSafe-Request-Id"]);
        Assert.NotNull(response.RawHttpResponse);
        Assert.Contains("\"answers\"", await response.RawHttpResponse!.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task MissingRequestIdIsNull()
    {
        using var client = Clients.Create(new StubHandler((_, _) => Http.Json(200, Http.SystemOneBody)));
        var response = await client.SystemOneAsync("x", Clients.SampleQuestions());
        Assert.Null(response.RequestId);
    }

    [Fact]
    public async Task UsageTokensMayBeMissingOrNull()
    {
        const string body = """{"model":"m","usage":{"input_tokens":null},"answers":{}}""";
        using var client = Clients.Create(new StubHandler((_, _) => Http.Json(200, body)));
        var response = await client.SystemOneAsync("x", Clients.SampleQuestions());
        Assert.Null(response.Usage.InputTokens);
        Assert.Null(response.Usage.OutputTokens);
        Assert.Empty(response.Answers);
    }

    [Fact]
    public async Task UnknownAnswerTypesAreSkippedWithAWarning()
    {
        const string body = """
            {"model":"m","usage":{},"answers":{
              "known":{"type":"noul","noul":0.5},
              "mystery":{"type":"rank","order":["a","b"]}
            }}
            """;
        var logger = new CapturingLogger();
        using var client = Clients.Create(new StubHandler((_, _) => Http.Json(200, body)), o =>
        {
            o.Logger = logger;
            o.LogLevel = LogLevel.Warning;
        });

        var response = await client.SystemOneAsync("x", Clients.SampleQuestions());

        Assert.Equal(0.5, response.Nouls["known"].Noul);
        var warning = Assert.Single(logger.Messages(LogLevel.Warning));
        Assert.Equal("Ignoring answer \"mystery\" with unrecognized type \"rank\"", warning);

        // The unmodelled answer is kept in Answers as a bare Answer, so a caller can see what the
        // server sent, but it reaches none of the typed views.
        Assert.Equal(2, response.Answers.Count);
        var mystery = response.Answers["mystery"];
        Assert.Equal(typeof(Answer), mystery.GetType());
        Assert.Equal("", mystery.Type);
        Assert.Single(response.Nouls);
        Assert.Empty(response.Choices);
        Assert.Empty(response.Scores);

        // Nothing the server sent is lost: the whole answer object, discriminator included, is in the
        // extension data, and the buffered response still holds the bytes it arrived in.
        Assert.Equal("rank", mystery.AdditionalProperties!["type"].GetString());
        Assert.Equal(JsonValueKind.Array, mystery.AdditionalProperties["order"].ValueKind);
        Assert.Equal(new[] { "order", "type" }, mystery.AdditionalProperties.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.NotNull(response.RawHttpResponse);
        var raw = await response.RawHttpResponse!.Content.ReadAsStringAsync();
        Assert.Contains("""{"type":"rank","order":["a","b"]}""", raw);
    }

    // Field paths are the reader's own path with the leading "$." stripped. A value of the wrong type
    // is reported at that value, but a missing required member is reported at the object that should
    // have carried it -- so an absent "confidence" is "answers.tone", and an absent top-level member
    // is the root, "".
    [Theory]
    [InlineData("""{"model":"m","usage":{},"answers":{"tone":{"type":"choice","choice":"calm","probabilities":{}}}}""", "answers.tone")]
    [InlineData("""{"model":"m","usage":{},"answers":{"tone":{"type":"choice","choice":"calm","confidence":"high","probabilities":{}}}}""", "answers.tone.confidence")]
    [InlineData("""{"model":"m","usage":{},"answers":{"b":{"type":"noul"}}}""", "answers.b")]
    [InlineData("""{"model":"m","usage":{},"answers":{"b":{"noul":0.1}}}""", "answers.b.type")]
    [InlineData("""{"model":"m","usage":{},"answers":{"b":"nope"}}""", "answers.b")]
    [InlineData("""{"model":"m","usage":{},"answers":{"s":{"type":"score","score":1,"confidence":1,"legend":{"x":"bad"},"probabilities":{}}}}""", "answers.s.legend.x")]
    [InlineData("""{"model":"m","usage":{},"answers":{"s":{"type":"score","score":1,"confidence":1,"legend":{},"probabilities":{"0":"n"}}}}""", "answers.s.probabilities.0")]
    [InlineData("""{"model":"m","usage":{"input_tokens":"many"},"answers":{}}""", "usage.input_tokens")]
    [InlineData("""{"model":"m","answers":{}}""", "")]
    [InlineData("""{"usage":{},"answers":{}}""", "")]
    [InlineData("""{"model":"m","usage":{}}""", "")]
    [InlineData("""[1,2]""", "")]
    [InlineData("not json at all", "")]
    public async Task MalformedSuccessBodiesRaiseValidationErrorsWithFieldPaths(string body, string expectedPath)
    {
        using var client = Clients.Create(new StubHandler((_, _) => Http.Json(200, body, ("x-typesafe-request-id", "r1"))));

        var error = await Assert.ThrowsAsync<TypeSafeApiResponseValidationException>(() =>
            client.SystemOneAsync("x", Clients.SampleQuestions()));

        Assert.Equal(expectedPath, error.FieldPath);
        Assert.Equal(HttpStatusCode.OK, error.StatusCode);
        Assert.Equal("r1", error.RequestId);
        Assert.Contains($"Invalid response data at '{expectedPath}'.", error.Message);
        Assert.StartsWith("POST ", error.Message);
        Assert.EndsWith("(request_id=r1)", error.Message);
    }

    [Fact]
    public async Task AMissingRequiredMemberIsAValidationErrorCarryingTheWholeResponse()
    {
        // "confidence" has no default, so the answer cannot be constructed -- and the reader's
        // JsonException must not escape as itself.
        const string body = """{"model":"m","usage":{},"answers":{"tone":{"type":"choice","choice":"calm","probabilities":{}}}}""";
        using var client = Clients.Create(new StubHandler((_, _) => Http.Json(200, body, ("x-typesafe-request-id", "r7"))));

        var error = await Assert.ThrowsAsync<TypeSafeApiResponseValidationException>(() =>
            client.SystemOneAsync("x", Clients.SampleQuestions()));

        Assert.Equal("answers.tone", error.FieldPath);
        Assert.Equal("Invalid response data at 'answers.tone'.", error.Detail);
        Assert.Equal(HttpStatusCode.OK, error.StatusCode);
        Assert.Equal("r7", error.RequestId);
        Assert.Equal("r7", error.Headers["X-TypeSafe-Request-Id"]);
        Assert.EndsWith("/v1/systemone", error.Endpoint);
        Assert.Equal("m", (string?)error.Body!["model"]);
    }

    [Fact]
    public async Task MalformedJsonIsAValidationErrorAtTheRootPath()
    {
        // A proxy answering 200 with an HTML page: nothing parses, so there is no field to blame.
        const string body = "<html><body>504 Gateway Time-out</body></html>";
        using var client = Clients.Create(new StubHandler((_, _) => Http.Json(200, body)));

        var error = await Assert.ThrowsAsync<TypeSafeApiResponseValidationException>(() =>
            client.SystemOneAsync("x", Clients.SampleQuestions()));

        Assert.Equal("", error.FieldPath);
        Assert.Contains("Invalid response data at ''.", error.Message);
        // Unparseable text is still reported as the body, the way an error body would be.
        Assert.Equal(body, (string?)error.Body);
    }

    [Fact]
    public async Task EmptySuccessBodyIsAValidationError()
    {
        using var client = Clients.Create(new StubHandler((_, _) => Http.Empty(200)));
        var error = await Assert.ThrowsAsync<TypeSafeApiResponseValidationException>(() => client.SystemOneAsync("x", Clients.SampleQuestions()));
        Assert.Equal("", error.FieldPath);
        Assert.Null(error.Body);
    }

    [Fact]
    public async Task ValidationErrorsAreNotRetried()
    {
        var handler = new StubHandler((_, _) => Http.Json(200, """{"model":"m"}"""));
        using var client = Clients.Create(handler);
        await Assert.ThrowsAsync<TypeSafeApiResponseValidationException>(() => client.SystemOneAsync("x", Clients.SampleQuestions()));
        Assert.Single(handler.Requests);
    }

    // List positions are bracketed, the way the reader writes them: "models[0]", not "models.0".
    [Theory]
    [InlineData("""{"models":"none"}""", "models")]
    [InlineData("""{"models":[{"name":"a","description":"b"}]}""", "models[0]")]
    [InlineData("""{"models":[1]}""", "models[0]")]
    [InlineData("""{}""", "")]
    public async Task MalformedModelListsRaiseValidationErrors(string body, string expectedPath)
    {
        using var client = Clients.Create(new StubHandler((_, _) => Http.Json(200, body)));
        var error = await Assert.ThrowsAsync<TypeSafeApiResponseValidationException>(() => client.Models.ListAsync());
        Assert.Equal(expectedPath, error.FieldPath);
    }

    // The models endpoint reports a validation error the same way the systemone endpoint does: the
    // whole exchange travels on the exception -- status, headers (and so the request id), endpoint,
    // and the body, parsed when it was JSON, a string node when it was not, and null when absent.
    [Fact]
    public async Task AModelListValidationErrorCarriesTheWholeResponse()
    {
        using var client = Clients.Create(new StubHandler((_, _) =>
            Http.Json(200, """{"models":"none"}""", ("x-typesafe-request-id", "r9"))));

        var error = await Assert.ThrowsAsync<TypeSafeApiResponseValidationException>(() => client.Models.ListAsync());

        Assert.Equal("models", error.FieldPath);
        Assert.Equal(HttpStatusCode.OK, error.StatusCode);
        Assert.Equal("r9", error.RequestId);
        Assert.Equal("r9", error.Headers["X-TypeSafe-Request-Id"]);
        Assert.StartsWith("GET ", error.Endpoint);
        Assert.EndsWith("/v1/models", error.Endpoint);
        Assert.Equal("none", (string?)error.Body!["models"]);

        using var textClient = Clients.Create(new StubHandler((_, _) => Http.Json(200, "not json at all")));
        var text = await Assert.ThrowsAsync<TypeSafeApiResponseValidationException>(() => textClient.Models.ListAsync());
        Assert.Equal("", text.FieldPath);
        Assert.Equal("not json at all", (string?)text.Body);

        using var emptyClient = Clients.Create(new StubHandler((_, _) => Http.Empty(200)));
        var empty = await Assert.ThrowsAsync<TypeSafeApiResponseValidationException>(() => emptyClient.Models.ListAsync());
        Assert.Equal("", empty.FieldPath);
        Assert.Null(empty.Body);
    }

    [Fact]
    public async Task ExtraFieldsInResponsesAreIgnored()
    {
        const string body = """{"model":"m","usage":{"input_tokens":1,"billing_units":3},"answers":{"b":{"type":"noul","noul":1,"extra":true}},"new_top_level":{}}""";
        using var client = Clients.Create(new StubHandler((_, _) => Http.Json(200, body)));
        var response = await client.SystemOneAsync("x", Clients.SampleQuestions());
        Assert.Equal(1, response.Nouls["b"].Noul);
    }

    [Fact]
    public async Task ModelListResponseIsItselfTheReadOnlyList()
    {
        using var client = Clients.Create(new StubHandler((_, _) => Http.Json(200, Http.ModelsBody)));

        var response = await client.Models.ListAsync();

        IReadOnlyList<ModelMetadata> list = response;
        Assert.Equal(2, response.Count);
        Assert.Equal(response.Count, list.Count);
        Assert.Equal("jev-latest", response[0].Name);
        Assert.Equal("jev-1", response[1].Name);

        var enumerated = new List<string>();
        foreach (var model in await client.Models.ListAsync()) enumerated.Add(model.Name);
        Assert.Equal(new[] { "jev-latest", "jev-1" }, enumerated);
        Assert.Equal(new[] { "jev-latest", "jev-1" }, response.Select(m => m.Name));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = response[2]);
    }

    [Fact]
    public async Task AnswerAliasesMirrorTheWireNames()
    {
        using var client = Clients.Create(new StubHandler((_, _) => Http.Json(200, Http.SystemOneBody)));

        var response = await client.SystemOneAsync("x", Clients.SampleQuestions());

        var billing = response.Nouls["billing"];
        Assert.Equal(billing.Noul, billing.Probability);
        Assert.Equal(0.93, billing.Probability);

        var tone = response.Choices["tone"];
        Assert.Equal(tone.Choice, tone.Label);
        Assert.Equal("frustrated", tone.Label);

        // Aliases are computed, so they never reach the wire.
        var noulJson = JsonSerializer.Serialize(new NoulAnswer(0.25));
        var choiceJson = JsonSerializer.Serialize(new ChoiceAnswer("calm", 0.5, new Dictionary<string, double>()));
        Assert.DoesNotContain("probability", noulJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("label", choiceJson, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Plain reflection-based options standing in for the source-generated
    /// <c>TypeSafeJsonContext</c>: the answer records themselves carry every mapping a reader needs.
    /// </summary>
    private static readonly JsonSerializerOptions PolymorphicOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowOutOfOrderMetadataProperties = true,
    };

    [Fact]
    public void AnswerRecordsDeserializePolymorphicallyByTypeDiscriminator()
    {
        var billing = Assert.IsType<NoulAnswer>(
            JsonSerializer.Deserialize<Answer>("""{"type":"noul","noul":0.93}""", PolymorphicOptions));
        Assert.Equal(0.93, billing.Noul);
        Assert.Equal(0.93, billing.Probability);
        Assert.Equal("noul", billing.Type);

        // "type" last: the discriminator may arrive out of order.
        var tone = Assert.IsType<ChoiceAnswer>(JsonSerializer.Deserialize<Answer>(
            """{"choice":"frustrated","confidence":0.81,"probabilities":{"calm":0.05,"angry":0.14},"type":"choice"}""",
            PolymorphicOptions));
        Assert.Equal("frustrated", tone.Choice);
        Assert.Equal("frustrated", tone.Label);
        Assert.Equal(0.81, tone.Confidence);
        Assert.Equal(0.14, tone.Probabilities["angry"]);
        Assert.Equal("choice", tone.Type);

        var urgency = Assert.IsType<ScoreAnswer>(JsonSerializer.Deserialize<Answer>(
            """{"type":"score","score":2.4,"confidence":0.7,"legend":{"0":"can wait","3":{"label":"right now"}},"probabilities":{"0":0.1,"3":0.5}}""",
            PolymorphicOptions));
        Assert.Equal(2.4, urgency.Score);
        Assert.Equal(0.7, urgency.Confidence);
        Assert.Equal(new[] { 0, 3 }, urgency.Legend.Keys.OrderBy(k => k));
        Assert.Equal("can wait", (string?)urgency.Legend[0]);
        Assert.Equal("right now", (string?)urgency.Legend[3]!["label"]);
        Assert.Equal(new[] { 0, 3 }, urgency.Probabilities.Keys.OrderBy(k => k));
        Assert.Equal(0.5, urgency.Probabilities[3]);
    }

    [Fact]
    public void UnmodelledAnswerFieldsAndTypesSurviveInExtensionData()
    {
        var known = Assert.IsType<NoulAnswer>(JsonSerializer.Deserialize<Answer>(
            """{"type":"noul","noul":0.5,"rationale":"mentions an invoice"}""", PolymorphicOptions));
        Assert.Equal(0.5, known.Noul);
        Assert.Equal("mentions an invoice", known.AdditionalProperties!["rationale"].GetString());

        // The base record is not abstract, so an answer with no discriminator of its own still
        // decodes, with every field in extension data. (An unrecognized discriminator is a read
        // error in System.Text.Json; AnswerMapConverter turns one into a bare Answer, which
        // SystemOneResponse.FromBody warns about and keeps out of the typed views.)
        var bare = JsonSerializer.Deserialize<Answer>("""{"order":["a","b"]}""", PolymorphicOptions);
        Assert.NotNull(bare);
        Assert.Equal(typeof(Answer), bare.GetType());
        Assert.Equal("", bare.Type);
        Assert.Equal(JsonValueKind.Array, bare.AdditionalProperties!["order"].ValueKind);
    }

    /// <summary>
    /// The declared shape of <see cref="Answer"/>, asserted on the metadata rather than on behaviour:
    /// a non-abstract record whose polymorphic mapping and extension-data bag are what let an answer
    /// this SDK version does not model still decode.
    /// </summary>
    [Fact]
    public void AnswerIsANonAbstractRecordWithThePolymorphicContractAndExtensionData()
    {
        Assert.False(typeof(Answer).IsAbstract);
        // Only a record gets the synthesized copy helper the with-expression calls.
        Assert.NotNull(typeof(Answer).GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance));

        var polymorphic = Assert.Single(typeof(Answer).GetCustomAttributes<JsonPolymorphicAttribute>(inherit: false));
        Assert.Equal("type", polymorphic.TypeDiscriminatorPropertyName);
        Assert.Equal(JsonUnknownDerivedTypeHandling.FallBackToBaseType, polymorphic.UnknownDerivedTypeHandling);

        var derived = typeof(Answer).GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false)
            .Select(attribute => (attribute.TypeDiscriminator as string, attribute.DerivedType))
            .OrderBy(pair => pair.Item1, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new (string?, Type)[]
        {
            ("choice", typeof(ChoiceAnswer)),
            ("noul", typeof(NoulAnswer)),
            ("score", typeof(ScoreAnswer)),
        }, derived);

        var extensionData = Assert.Single(typeof(Answer).GetProperties(), p => p.IsDefined(typeof(JsonExtensionDataAttribute), inherit: false));
        Assert.Equal(nameof(Answer.AdditionalProperties), extensionData.Name);
        Assert.Equal(typeof(IDictionary<string, JsonElement>), extensionData.PropertyType);
    }

    [Fact]
    public void ResponsesCanBeConstructedForTestDoubles()
    {
        var response = new SystemOneResponse("m", new Usage(1, 2), new Dictionary<string, Answer>
        {
            ["yes"] = new NoulAnswer(0.9),
        });
        Assert.Null(response.RequestId);
        Assert.Null(response.RawHttpResponse);
        Assert.Equal((HttpStatusCode)0, response.StatusCode);
        Assert.Equal(0.9, response.Nouls["yes"].Noul);
    }
}
