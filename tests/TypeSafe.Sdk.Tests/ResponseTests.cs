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
        Assert.Equal(200, response.Status);
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

        Assert.Single(response.Answers);
        Assert.Equal(0.5, response.Nouls["known"].Noul);
        var warning = Assert.Single(logger.Messages(LogLevel.Warning));
        Assert.Contains("\"mystery\"", warning);
        Assert.Contains("\"rank\"", warning);
    }

    [Theory]
    [InlineData("""{"model":"m","usage":{},"answers":{"tone":{"type":"choice","choice":"calm","probabilities":{}}}}""", "answers.tone.confidence")]
    [InlineData("""{"model":"m","usage":{},"answers":{"tone":{"type":"choice","choice":"calm","confidence":"high","probabilities":{}}}}""", "answers.tone.confidence")]
    [InlineData("""{"model":"m","usage":{},"answers":{"b":{"type":"noul"}}}""", "answers.b.noul")]
    [InlineData("""{"model":"m","usage":{},"answers":{"b":{"noul":0.1}}}""", "answers.b.type")]
    [InlineData("""{"model":"m","usage":{},"answers":{"b":"nope"}}""", "answers.b")]
    [InlineData("""{"model":"m","usage":{},"answers":{"s":{"type":"score","score":1,"confidence":1,"legend":{"x":"bad"},"probabilities":{}}}}""", "answers.s.legend.x")]
    [InlineData("""{"model":"m","usage":{},"answers":{"s":{"type":"score","score":1,"confidence":1,"legend":{},"probabilities":{"0":"n"}}}}""", "answers.s.probabilities.0")]
    [InlineData("""{"model":"m","usage":{"input_tokens":"many"},"answers":{}}""", "usage.input_tokens")]
    [InlineData("""{"model":"m","answers":{}}""", "usage")]
    [InlineData("""{"usage":{},"answers":{}}""", "model")]
    [InlineData("""{"model":"m","usage":{}}""", "answers")]
    [InlineData("""[1,2]""", "")]
    [InlineData("not json at all", "")]
    public async Task MalformedSuccessBodiesRaiseValidationErrorsWithFieldPaths(string body, string expectedPath)
    {
        using var client = Clients.Create(new StubHandler((_, _) => Http.Json(200, body, ("x-typesafe-request-id", "r1"))));

        var error = await Assert.ThrowsAsync<TypeSafeApiResponseValidationException>(() =>
            client.SystemOneAsync("x", Clients.SampleQuestions()));

        Assert.Equal(expectedPath, error.FieldPath);
        Assert.Equal(200, error.Status);
        Assert.Equal("r1", error.RequestId);
        Assert.Contains($"Invalid response data at '{expectedPath}'.", error.Message);
        Assert.StartsWith("POST ", error.Message);
        Assert.EndsWith("(request_id=r1)", error.Message);
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

    [Theory]
    [InlineData("""{"models":"none"}""", "models")]
    [InlineData("""{"models":[{"name":"a","description":"b"}]}""", "models.0.release_date")]
    [InlineData("""{"models":[1]}""", "models.0")]
    [InlineData("""{}""", "models")]
    public async Task MalformedModelListsRaiseValidationErrors(string body, string expectedPath)
    {
        using var client = Clients.Create(new StubHandler((_, _) => Http.Json(200, body)));
        var error = await Assert.ThrowsAsync<TypeSafeApiResponseValidationException>(() => client.Models.ListAsync());
        Assert.Equal(expectedPath, error.FieldPath);
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
    public void ResponsesCanBeConstructedForTestDoubles()
    {
        var response = new SystemOneResponse("m", new Usage(1, 2), new Dictionary<string, Answer>
        {
            ["yes"] = new NoulAnswer(0.9),
        });
        Assert.Null(response.RequestId);
        Assert.Null(response.RawHttpResponse);
        Assert.Equal(0, response.Status);
        Assert.Equal(0.9, response.Nouls["yes"].Noul);
    }
}
