using System.Text.Json;
using System.Text.Json.Nodes;
using TypeSafe.Internal;

namespace TypeSafe.Tests;

public class RequestTests
{
    [Fact]
    public async Task SystemOneSendsProtectedHeadersAndJsonBody()
    {
        var handler = new StubHandler((_, _) => Http.Json(200, Http.SystemOneBody));
        using var client = Clients.Create(handler, o => o.BaseUrl = "https://api.example/");

        await client.SystemOneAsync("I was charged twice.", Clients.SampleQuestions());

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.example/v1/systemone", request.Url.ToString());
        Assert.Equal($"Bearer {Clients.ApiKey}", request.Header("Authorization"));
        Assert.Equal("application/json", request.Header("Accept"));
        Assert.Equal("application/json", request.Header("Content-Type"));
        Assert.Equal($"typesafe-sdk/{TypeSafeConstants.Version}", request.Header("User-Agent"));
        Assert.Equal($"typesafe-sdk/{TypeSafeConstants.Version}", request.Header("X-TypeSafe-SDK"));
        Assert.StartsWith("dotnet/", request.Header("X-TypeSafe-Runtime"));
        Assert.Null(request.Header("X-TypeSafe-Retry-Count"));

        var body = Assert.IsType<JsonObject>(request.Json);
        Assert.Equal("I was charged twice.", (string?)body["state"]);
        Assert.Equal(TypeSafeConstants.DefaultModel, (string?)body["model"]);
        var questions = Assert.IsType<JsonObject>(body["questions"]);
        Assert.Equal(new[] { "billing", "tone", "urgency" }, questions.Select(q => q.Key));
    }

    [Fact]
    public async Task ModelOverrideReplacesDefaultModel()
    {
        var handler = new StubHandler((_, _) => Http.Json(200, Http.SystemOneBody));
        using var client = Clients.Create(handler, o => o.DefaultModel = "client-model");

        await client.SystemOneAsync("x", Clients.SampleQuestions());
        await client.SystemOneAsync("x", Clients.SampleQuestions(), model: "call-model");

        Assert.Equal("client-model", (string?)handler.Requests[0].Json!["model"]);
        Assert.Equal("call-model", (string?)handler.Requests[1].Json!["model"]);
    }

    [Fact]
    public async Task StateAcceptsObjectsStringsAndJsonNodes()
    {
        var handler = new StubHandler((_, _) => Http.Json(200, Http.SystemOneBody));
        using var client = Clients.Create(handler);
        var questions = Clients.SampleQuestions();

        await client.SystemOneAsync(Content.From(new { Document = "hello", Tags = new[] { "a", "b" } }), questions);
        await client.SystemOneAsync("plain text", questions);
        await client.SystemOneAsync(new JsonArray("x", 1), questions);
        await client.SystemOneAsync(Content.From(new Dictionary<string, object?> { ["k"] = null }), questions);
        await client.SystemOneAsync(Content.Null, questions);

        Assert.Equal("""{"document":"hello","tags":["a","b"]}""", handler.Requests[0].Json!["state"]!.ToJsonString());
        Assert.Equal("plain text", (string?)handler.Requests[1].Json!["state"]);
        Assert.Equal("""["x",1]""", handler.Requests[2].Json!["state"]!.ToJsonString());
        Assert.Equal("""{"k":null}""", handler.Requests[3].Json!["state"]!.ToJsonString());
        var withNull = Assert.IsType<JsonObject>(handler.Requests[4].Json);
        Assert.True(withNull.ContainsKey("state"));
        Assert.Null(withNull["state"]);
    }

    [Fact]
    public void StateSerializationUsesTheSharedWebOptionsSingleton()
    {
        Assert.Same(JsonSerializerOptions.Web, JsonContent.SerializerOptions);
        Assert.True(JsonContent.SerializerOptions.IsReadOnly);
        Assert.Equal("""{"twoWords":1}""", JsonContent.From(new { TwoWords = 1 })!.ToJsonString());
    }

    [Fact]
    public async Task ExtraBodyIsShallowMergedLastWriteWins()
    {
        var handler = new StubHandler((_, _) => Http.Json(200, Http.SystemOneBody));
        using var client = Clients.Create(handler);

        await client.SystemOneAsync(new SystemOneRequest("x", Clients.SampleQuestions())
        {
            ExtraBody = new Dictionary<string, JsonNode?>
            {
                ["temperature"] = 0.2,
                ["model"] = "override-model",
                ["nothing"] = null,
            },
        });

        var body = handler.Requests[0].Json!;
        Assert.Equal(0.2, (double?)body["temperature"]);
        Assert.Equal("override-model", (string?)body["model"]);
        Assert.True(((JsonObject)body).ContainsKey("nothing"));
    }

    [Fact]
    public async Task RawQuestionsReachTheWireVerbatim()
    {
        var handler = new StubHandler((_, _) => Http.Json(200, Http.SystemOneBody));
        using var client = Clients.Create(handler, o => o.DefaultModel = "wire-model");

        await client.SystemOneAsync("x", new Questions
        {
            ["tone"] = Question.FromJson(new JsonObject
            {
                ["type"] = "choice",
                ["instructions"] = "Tone?",
                ["criteria"] = new JsonObject { ["calm"] = null },
                ["future_field"] = 42,
            }),
        });

        // The whole envelope, so the raw question's unmodelled field and the key order of both the
        // question and the body around it are asserted on the bytes that left the client.
        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            """{"state":"x","model":"wire-model","questions":{"tone":{"type":"choice","instructions":"Tone?","criteria":{"calm":null},"future_field":42}}}""",
            request.Body);
    }

    [Fact]
    public async Task UserHeadersMergeCaseInsensitivelyAndCannotClobberProtectedOnes()
    {
        var handler = new StubHandler((_, _) => Http.Json(200, Http.SystemOneBody));
        using var client = Clients.Create(handler, o => o.DefaultHeaders = new Dictionary<string, string>
        {
            ["X-Team"] = "default",
            ["X-Trace"] = "default-trace",
            ["Authorization"] = "Bearer attacker",
            ["x-typesafe-retry-count"] = "9",
        });

        await client.SystemOneAsync("x", Clients.SampleQuestions(), options: new RequestOptions
        {
            Headers = new Dictionary<string, string>
            {
                ["x-team"] = "per-call",
                ["accept"] = "text/html",
                ["content-type"] = "text/plain",
                ["User-Agent"] = "custom",
            },
        });

        var request = handler.Requests[0];
        Assert.Equal("per-call", request.Header("X-Team"));
        Assert.Equal("default-trace", request.Header("X-Trace"));
        Assert.Equal($"Bearer {Clients.ApiKey}", request.Header("Authorization"));
        Assert.Equal("application/json", request.Header("Accept"));
        Assert.Equal("application/json", request.Header("Content-Type"));
        Assert.Equal($"typesafe-sdk/{TypeSafeConstants.Version}", request.Header("User-Agent"));
        Assert.Null(request.Header("X-TypeSafe-Retry-Count"));
    }

    [Fact]
    public async Task ModelsListIsAGetWithoutBodyOrContentType()
    {
        var handler = new StubHandler((_, _) => Http.Json(200, Http.ModelsBody));
        using var client = Clients.Create(handler);

        var response = await client.Models.ListAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.EndsWith("/v1/models", request.Url.ToString());
        Assert.Null(request.Body);
        Assert.Null(request.Header("Content-Type"));
        Assert.Equal(2, response.Count);
        Assert.Equal("jev-latest", response[0].Name);
        Assert.Equal("Latest model", response[0].Description);
        Assert.Equal("2026-09-01", response[0].ReleaseDate);
    }

    [Fact]
    public async Task EmptyQuestionsAreRejectedBeforeSending()
    {
        var handler = new StubHandler((_, _) => Http.Json(200, Http.SystemOneBody));
        using var client = Clients.Create(handler);

        var error = await Assert.ThrowsAsync<TypeSafeException>(() => client.SystemOneAsync("x", new Questions()));

        Assert.Equal("At least one question is required.", error.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ConcurrentRequestsAreIndependent()
    {
        var handler = new StubHandler((_, _) => Http.Json(200, Http.SystemOneBody));
        using var client = Clients.Create(handler);

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.SystemOneAsync("x", Clients.SampleQuestions())));

        Assert.Equal(8, handler.Requests.Count);
        Assert.All(responses, r => Assert.Equal(0.93, r.Nouls["billing"].Noul));
    }
}
